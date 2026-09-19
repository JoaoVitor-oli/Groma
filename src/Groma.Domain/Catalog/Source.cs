using Groma.Domain.Exceptions;

namespace Groma.Domain.Catalog;

/// <summary>
/// Uma fonte cadastrada. Guarda os datasets que produz: um só, no caso de um
/// arquivo enviado pela interface; vários, no caso de um banco com várias tabelas.
/// É a raiz do agregado — datasets nascem e são removidos por aqui.
/// </summary>
public sealed class Source
{
    private readonly List<Dataset> _datasets = [];

    private Source(string name, ConnectionSettings connection, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Connection = connection;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public ConnectionSettings Connection { get; private set; }

    public SourceKind Kind => Connection.Kind;

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Fonte inativa não é agendada nem aceita novas cargas.</summary>
    public bool IsActive { get; private set; }

    public IReadOnlyList<Dataset> Datasets => _datasets;

    public static Source Create(string name, ConnectionSettings connection, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome da fonte não pode ser vazio.");

        return new Source(name.Trim(), connection, createdAt);
    }

    /// <summary>
    /// Cria um dataset sob esta fonte. Nomes são únicos dentro da fonte, sem
    /// diferenciar maiúsculas — "Vendas" e "vendas" seriam a mesma tabela.
    /// </summary>
    public Dataset AddDataset(string name, DateTimeOffset at)
    {
        if (!IsActive)
            throw new DomainException($"Fonte '{Name}' está inativa e não aceita novos datasets.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do dataset não pode ser vazio.");

        var trimmed = name.Trim();

        if (_datasets.Any(d => string.Equals(d.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"A fonte '{Name}' já tem um dataset chamado '{trimmed}'.");

        var dataset = new Dataset(Id, trimmed, at);
        _datasets.Add(dataset);

        return dataset;
    }

    public Dataset? FindDataset(string name)
        => _datasets.FirstOrDefault(d =>
            string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Troca os dados de conexão — rotação de credencial, servidor que mudou de
    /// endereço. O tipo da fonte não muda: um banco não vira uma API.
    /// </summary>
    public void UpdateConnection(ConnectionSettings connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.Kind != Connection.Kind)
            throw new DomainException(
                $"Fonte '{Name}' é do tipo {Connection.Kind} e não pode virar {connection.Kind}.");

        Connection = connection;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome da fonte não pode ser vazio.");

        Name = name.Trim();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
