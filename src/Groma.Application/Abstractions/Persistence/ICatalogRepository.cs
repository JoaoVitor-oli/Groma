using Groma.Domain.Catalog;

namespace Groma.Application.Abstractions.Persistence;

/// <summary>
/// Acesso ao catálogo de metadados — fontes, datasets e versões de schema.
/// É o mundo de schema fixo, implementado com EF Core.
/// </summary>
public interface ICatalogRepository
{
    Task<Source?> FindSourceAsync(Guid sourceId, CancellationToken cancellationToken);

    Task<DatasetVersion?> FindCurrentVersionAsync(Guid datasetId, CancellationToken cancellationToken);

    Task AddAsync(Source source, CancellationToken cancellationToken);

    Task AddAsync(DatasetVersion version, CancellationToken cancellationToken);
}

/// <summary>
/// Fecha a transação do catálogo. Separado do repositório para que um caso de
/// uso possa fazer várias alterações e gravar todas de uma vez.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
