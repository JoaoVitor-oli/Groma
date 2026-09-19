using Groma.Application.Abstractions;
using Groma.Application.Abstractions.Persistence;
using Groma.Domain.Catalog;

namespace Groma.Application.Ingestion.Pipeline;

/// <summary>
/// Confronta o schema inferido com a versão registrada e decide o que acontece:
/// seguir na mesma versão, criar a próxima, ou abortar a carga.
/// </summary>
/// <remarks>
/// Este estágio não decide nada por conta própria — ele consulta o domínio e
/// obedece. A política de o que é compatível vive em <see cref="SchemaComparison"/>.
/// </remarks>
public sealed class ResolveVersionStage(
    ICatalogRepository catalog,
    IClock clock) : IIngestionStage
{
    public async Task ExecuteAsync(IngestionContext context, CancellationToken cancellationToken)
    {
        var current = await catalog.FindCurrentVersionAsync(context.Dataset.Id, cancellationToken);

        // Primeira carga: nada com que comparar, tudo é coluna nova.
        var comparison = current is null
            ? SchemaComparison.Between([], context.InferredColumns)
            : current.Compare(context.InferredColumns);

        context.Comparison = comparison;

        if (!comparison.CanProceed)
        {
            context.Aborted = true;
            return;
        }

        if (current is not null && !comparison.RequiresNewVersion)
        {
            context.Version = current;
            return;
        }

        var next = current is null
            ? new DatasetVersion(context.Dataset.Id, 1, context.InferredColumns, clock.UtcNow)
            : current.Next(context.InferredColumns, clock.UtcNow);

        await catalog.AddAsync(next, cancellationToken);

        context.Version = next;
    }
}
