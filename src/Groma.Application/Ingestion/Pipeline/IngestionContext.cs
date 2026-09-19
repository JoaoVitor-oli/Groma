using Groma.Application.Abstractions.Ingestion;
using Groma.Domain.Catalog;

namespace Groma.Application.Ingestion.Pipeline;

/// <param name="PayloadRef">
/// Ponteiro para o conteúdo desta carga. Caminho do arquivo enviado, no caso de
/// upload; string vazia para fontes que se conectam sozinhas.
/// </param>
public sealed record IngestionRequest(Guid SourceId, string DatasetName, string PayloadRef = "");

public sealed record IngestionOutcome(
    Guid DatasetId,
    SchemaVerdict Verdict,
    int VersionNumber,
    long RowsLoaded,
    IReadOnlyList<ColumnChange> Changes)
{
    public bool Blocked => Verdict is SchemaVerdict.Breaking;
}

/// <summary>
/// O estado que atravessa os estágios. Cada estágio lê o que os anteriores
/// deixaram e acrescenta o seu. Manter isso explícito é o que permite rodar
/// um subconjunto dos estágios num reprocessamento.
/// </summary>
public sealed class IngestionContext(IngestionRequest request, Source source, Dataset dataset)
{
    public IngestionRequest Request { get; } = request;

    public Source Source { get; } = source;

    public Dataset Dataset { get; } = dataset;

    /// <summary>Preenchido pelo ReadStage.</summary>
    public IReadOnlyList<string> Header { get; internal set; } = [];

    /// <summary>Primeiras linhas, guardadas para a inferência. Preenchido pelo ReadStage.</summary>
    public IReadOnlyList<RawRow> Sample { get; internal set; } = [];

    /// <summary>
    /// Todas as linhas, incluindo as da amostra. Preenchido pelo ReadStage.
    /// Só pode ser percorrido uma vez.
    /// </summary>
    public IAsyncEnumerable<RawRow>? Rows { get; internal set; }

    /// <summary>Preenchido pelo InferSchemaStage.</summary>
    public IReadOnlyList<ColumnDefinition> InferredColumns { get; internal set; } = [];

    /// <summary>Preenchido pelo ResolveVersionStage.</summary>
    public SchemaComparison? Comparison { get; internal set; }

    /// <summary>A versão contra a qual esta carga vai gravar. Preenchido pelo ResolveVersionStage.</summary>
    public DatasetVersion? Version { get; internal set; }

    /// <summary>Preenchido pelo PersistStage.</summary>
    public long RowsLoaded { get; internal set; }

    /// <summary>Marcado quando o domínio barra a carga. Os estágios seguintes não rodam.</summary>
    public bool Aborted { get; internal set; }

    public IngestionOutcome ToOutcome() => new(
        Dataset.Id,
        Comparison?.Verdict ?? SchemaVerdict.Identical,
        Version?.Number ?? Dataset.CurrentVersionNumber,
        RowsLoaded,
        Comparison?.Changes ?? []);
}
