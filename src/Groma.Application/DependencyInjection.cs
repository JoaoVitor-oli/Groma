using Groma.Application.Ingestion.Pipeline;
using Groma.Application.Ingestion.StartIngestion;
using Microsoft.Extensions.DependencyInjection;

namespace Groma.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra os casos de uso e o pipeline. A ordem de registro dos estágios
    /// é a ordem de execução — trocar duas linhas aqui muda o comportamento da
    /// ingestão, então ela é deliberada e não alfabética.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IIngestionStage, ReadStage>();
        services.AddScoped<IIngestionStage, InferSchemaStage>();
        services.AddScoped<IIngestionStage, ResolveVersionStage>();
        services.AddScoped<IIngestionStage, PersistStage>();

        services.AddScoped<IngestionPipeline>();
        services.AddScoped<StartIngestionHandler>();

        return services;
    }
}
