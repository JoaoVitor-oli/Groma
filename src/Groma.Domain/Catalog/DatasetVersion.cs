using Groma.Domain.Exceptions;

namespace Groma.Domain.Catalog;

/// <summary>
/// Um schema congelado no tempo. Versões nunca são editadas: quando a fonte muda,
/// nasce a próxima. É o que permite responder "como essa tabela era em março".
/// </summary>
public sealed class DatasetVersion
{
    private readonly List<ColumnDefinition> _columns;

    public DatasetVersion(
        Guid datasetId,
        int number,
        IEnumerable<ColumnDefinition> columns,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(columns);

        if (datasetId == Guid.Empty)
            throw new DomainException("Versão precisa pertencer a um dataset.");

        if (number < 1)
            throw new DomainException("Número da versão começa em 1.");

        _columns = [.. columns];

        if (_columns.Count == 0)
            throw new DomainException("Versão de schema precisa de ao menos uma coluna.");

        var duplicate = _columns
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
            throw new DomainException($"Coluna '{duplicate.Key}' aparece mais de uma vez no schema.");

        Id = Guid.CreateVersion7();
        DatasetId = datasetId;
        Number = number;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid DatasetId { get; }

    /// <summary>Sequencial dentro do dataset: v1, v2, v3.</summary>
    public int Number { get; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<ColumnDefinition> Columns => _columns;

    /// <summary>
    /// Confronta este schema com o que a carga atual encontrou na fonte.
    /// </summary>
    public SchemaComparison Compare(IReadOnlyList<ColumnDefinition> inferred)
        => SchemaComparison.Between(_columns, inferred);

    /// <summary>
    /// Deriva a próxima versão. Só faz sentido quando a comparação pediu uma.
    /// </summary>
    public DatasetVersion Next(IReadOnlyList<ColumnDefinition> columns, DateTimeOffset at)
        => new(DatasetId, Number + 1, columns, at);
}
