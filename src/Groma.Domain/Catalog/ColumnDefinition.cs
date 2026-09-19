using Groma.Domain.Exceptions;

namespace Groma.Domain.Catalog;

/// <summary>
/// Uma coluna dentro de uma versão de schema. Value object: duas definições com
/// os mesmos valores são a mesma coisa.
/// </summary>
public sealed record ColumnDefinition
{
    public ColumnDefinition(string name, DataType type, bool isNullable, int ordinal)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome de coluna não pode ser vazio.");

        if (ordinal < 0)
            throw new DomainException($"Ordinal da coluna '{name}' não pode ser negativo.");

        Name = name.Trim();
        Type = type;
        IsNullable = isNullable;
        Ordinal = ordinal;
    }

    /// <summary>Nome como veio da fonte, sem normalização. O quoting é problema da infraestrutura.</summary>
    public string Name { get; }

    public DataType Type { get; }

    public bool IsNullable { get; }

    /// <summary>Posição na fonte. Não participa da compatibilidade: reordenar coluna não quebra nada.</summary>
    public int Ordinal { get; }
}
