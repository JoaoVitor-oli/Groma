using Groma.Application.Abstractions.Ingestion;

namespace Groma.Application.Ingestion.Pipeline;

/// <summary>
/// Traduz a amostra em definições de coluna. Toda a heurística de tipo mora na
/// implementação de <see cref="ISchemaInferrer"/>, na infraestrutura.
/// </summary>
public sealed class InferSchemaStage(ISchemaInferrer inferrer) : IIngestionStage
{
    public Task ExecuteAsync(IngestionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        context.InferredColumns = inferrer.Infer(context.Header, context.Sample);

        return Task.CompletedTask;
    }
}
