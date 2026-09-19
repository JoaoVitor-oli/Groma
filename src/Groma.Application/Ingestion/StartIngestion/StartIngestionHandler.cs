using Groma.Application.Abstractions;
using Groma.Application.Abstractions.Persistence;
using Groma.Application.Ingestion.Pipeline;
using Groma.Domain.Catalog;
using Groma.Domain.Exceptions;

namespace Groma.Application.Ingestion.StartIngestion;

public sealed record StartIngestionCommand(
    Guid SourceId,
    string DatasetName,
    string PayloadRef = "");

/// <summary>
/// Ponto de entrada da ingestão. Resolve a fonte e o dataset, monta o contexto,
/// roda o pipeline e fecha a transação do catálogo.
/// </summary>
public sealed class StartIngestionHandler(
    ICatalogRepository catalog,
    IUnitOfWork unitOfWork,
    IngestionPipeline pipeline,
    IClock clock)
{
    public async Task<IngestionOutcome> HandleAsync(
        StartIngestionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var source = await catalog.FindSourceAsync(command.SourceId, cancellationToken)
            ?? throw new DomainException($"Fonte {command.SourceId} não encontrada.");

        if (!source.IsActive)
            throw new DomainException($"Fonte '{source.Name}' está inativa.");

        // Dataset é criado na primeira carga com aquele nome, e reaproveitado depois.
        var dataset = source.FindDataset(command.DatasetName)
            ?? source.AddDataset(command.DatasetName, clock.UtcNow);

        var context = new IngestionContext(
            new IngestionRequest(command.SourceId, dataset.Name, command.PayloadRef),
            source,
            dataset);

        var outcome = await pipeline.RunAsync(context, cancellationToken);

        // Grava mesmo quando a carga foi barrada: o dataset recém-criado e o
        // registro da tentativa continuam valendo.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return outcome;
    }
}
