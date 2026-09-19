namespace Groma.Application.Ingestion.Pipeline;

/// <summary>
/// Uma etapa da ingestão. Lê o que os estágios anteriores deixaram no contexto
/// e acrescenta o seu resultado. Estágios não se conhecem entre si — quem define
/// a ordem é o <see cref="IngestionPipeline"/>.
/// </summary>
public interface IIngestionStage
{
    Task ExecuteAsync(IngestionContext context, CancellationToken cancellationToken);
}
