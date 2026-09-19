namespace Groma.Application.Ingestion.Pipeline;

/// <summary>
/// Roda os estágios em ordem e para no primeiro que abortar.
/// </summary>
/// <remarks>
/// Não sabe ler arquivo, inferir tipo nem gravar linha — só a sequência.
/// Por isso acrescentar o profiling na fatia 2 é acrescentar um estágio ao
/// final da lista, e reprocessar só o perfil é chamar o pipeline com uma
/// lista menor de estágios.
/// </remarks>
public sealed class IngestionPipeline(IEnumerable<IIngestionStage> stages)
{
    private readonly IReadOnlyList<IIngestionStage> _stages = [.. stages];

    public async Task<IngestionOutcome> RunAsync(
        IngestionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var stage in _stages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await stage.ExecuteAsync(context, cancellationToken);

            if (context.Aborted)
                break;
        }

        return context.ToOutcome();
    }
}
