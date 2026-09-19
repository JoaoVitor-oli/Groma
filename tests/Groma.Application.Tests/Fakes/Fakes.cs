using Groma.Application.Abstractions;
using Groma.Application.Abstractions.Ingestion;
using Groma.Application.Abstractions.Persistence;
using Groma.Domain.Catalog;
using System.Runtime.CompilerServices;

namespace Groma.Application.Tests.Fakes;

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

public sealed class FakeCatalogRepository : ICatalogRepository
{
    private readonly Dictionary<Guid, Source> _sources = [];
    private readonly List<DatasetVersion> _versions = [];

    public int SaveCount { get; private set; }

    public IReadOnlyList<DatasetVersion> Versions => _versions;

    public void Seed(Source source) => _sources[source.Id] = source;

    public void Seed(DatasetVersion version) => _versions.Add(version);

    public Task<Source?> FindSourceAsync(Guid sourceId, CancellationToken cancellationToken)
        => Task.FromResult(_sources.GetValueOrDefault(sourceId));

    public Task<DatasetVersion?> FindCurrentVersionAsync(Guid datasetId, CancellationToken cancellationToken)
        => Task.FromResult(_versions
            .Where(v => v.DatasetId == datasetId)
            .OrderByDescending(v => v.Number)
            .FirstOrDefault());

    public Task AddAsync(Source source, CancellationToken cancellationToken)
    {
        _sources[source.Id] = source;
        return Task.CompletedTask;
    }

    public Task AddAsync(DatasetVersion version, CancellationToken cancellationToken)
    {
        _versions.Add(version);
        return Task.CompletedTask;
    }

    public Task<int> SaveAsync()
    {
        SaveCount++;
        return Task.FromResult(0);
    }
}

public sealed class FakeUnitOfWork(FakeCatalogRepository catalog) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => catalog.SaveAsync();
}

public sealed class FakeDataStore : IDataStore
{
    public TableSpec? LastTable { get; private set; }

    public List<RawRow> Written { get; } = [];

    public int EnsureTableCalls { get; private set; }

    public Task EnsureTableAsync(TableSpec table, CancellationToken cancellationToken)
    {
        LastTable = table;
        EnsureTableCalls++;
        return Task.CompletedTask;
    }

    public async Task<long> ReplaceAsync(
        TableSpec table,
        IAsyncEnumerable<RawRow> rows,
        CancellationToken cancellationToken)
    {
        LastTable = table;
        Written.Clear();

        await foreach (var row in rows.WithCancellation(cancellationToken))
            Written.Add(row);

        return Written.Count;
    }
}

/// <summary>
/// Leitor que devolve linhas de uma lista em memória, contando quantas vezes foi
/// aberto — é assim que o teste prova que a fonte é lida uma única vez.
/// </summary>
public sealed class FakeSourceReader(IReadOnlyList<string> header, IReadOnlyList<RawRow> rows)
    : ISourceReader, ISourceReaderFactory
{
    public int OpenCount { get; private set; }

    public bool Disposed { get; private set; }

    public ISourceReader Open(Source source, string payloadRef)
    {
        OpenCount++;
        return this;
    }

    public Task<IReadOnlyList<string>> ReadHeaderAsync(CancellationToken cancellationToken)
        => Task.FromResult(header);

    public async IAsyncEnumerable<RawRow> ReadRowsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return row;
        }
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>Devolve sempre o schema que o teste mandar, sem heurística nenhuma.</summary>
public sealed class FakeSchemaInferrer(IReadOnlyList<ColumnDefinition> columns) : ISchemaInferrer
{
    public IReadOnlyList<RawRow>? SampleSeen { get; private set; }

    public IReadOnlyList<ColumnDefinition> Infer(IReadOnlyList<string> header, IReadOnlyList<RawRow> sample)
    {
        SampleSeen = sample;
        return columns;
    }
}
