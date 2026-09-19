using Groma.Application.Abstractions;
using Groma.Application.Abstractions.Persistence;
using Groma.Domain.Catalog;

namespace Groma.Application.Ingestion.Pipeline;

/// <summary>
/// Garante a tabela física e grava as linhas.
/// </summary>
public sealed class PersistStage(IDataStore store, IClock clock) : IIngestionStage
{
    public const string DataSchema = "data";

    public async Task ExecuteAsync(IngestionContext context, CancellationToken cancellationToken)
    {
        if (context.Version is null || context.Rows is null)
            throw new InvalidOperationException("PersistStage precisa de uma versão resolvida e de um fluxo de linhas.");

        var table = TableFor(context.Dataset, context.Version);

        await store.EnsureTableAsync(table, cancellationToken);

        context.RowsLoaded = await store.ReplaceAsync(table, context.Rows, cancellationToken);

        context.Dataset.RecordIngestion(context.Version.Number, clock.UtcNow);
    }

    /// <summary>
    /// O nome físico vem do id do dataset, nunca do nome que a pessoa deu.
    /// Assim renomear na interface não renomeia tabela, dois datasets homônimos
    /// de fontes diferentes não colidem, e texto vindo de arquivo de terceiro
    /// nunca vira identificador SQL.
    /// </summary>
    public static TableSpec TableFor(Dataset dataset, DatasetVersion version)
        => new(DataSchema, $"ds_{dataset.Id:N}", version.Columns);
}
