namespace Groma.Domain.Catalog;

public enum ColumnChangeKind
{
    Added,
    Removed,
    TypeWidened,
    TypeNarrowed,
    BecameNullable,
    BecameRequired
}

public sealed record ColumnChange(
    string ColumnName,
    ColumnChangeKind Kind,
    DataType? From = null,
    DataType? To = null);

public enum SchemaVerdict
{
    /// <summary>Nada mudou. A carga segue na versão atual.</summary>
    Identical,

    /// <summary>Mudou, mas nada se perde. Gera nova versão e a carga segue.</summary>
    Compatible,

    /// <summary>Mudança destrutiva. A carga para e espera decisão de quem cuida da fonte.</summary>
    Breaking
}

/// <summary>
/// Diferença entre o schema registrado e o schema que a carga atual encontrou.
/// É a decisão central do Groma, e é lógica pura: não conhece banco, arquivo nem HTTP.
/// </summary>
public sealed class SchemaComparison
{
    private SchemaComparison(SchemaVerdict verdict, IReadOnlyList<ColumnChange> changes)
    {
        Verdict = verdict;
        Changes = changes;
    }

    public SchemaVerdict Verdict { get; }

    public IReadOnlyList<ColumnChange> Changes { get; }

    public bool RequiresNewVersion => Verdict is not SchemaVerdict.Identical;

    public bool CanProceed => Verdict is not SchemaVerdict.Breaking;

    public static SchemaComparison Between(
        IReadOnlyList<ColumnDefinition> current,
        IReadOnlyList<ColumnDefinition> incoming)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(incoming);

        var currentByName = current.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var incomingByName = incoming.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var changes = new List<ColumnChange>();

        foreach (var old in current)
        {
            if (!incomingByName.TryGetValue(old.Name, out var now))
            {
                changes.Add(new ColumnChange(old.Name, ColumnChangeKind.Removed, old.Type));
                continue;
            }

            if (old.Type != now.Type)
            {
                var kind = old.Type.CanWidenTo(now.Type)
                    ? ColumnChangeKind.TypeWidened
                    : ColumnChangeKind.TypeNarrowed;

                changes.Add(new ColumnChange(old.Name, kind, old.Type, now.Type));
            }

            if (old.IsNullable != now.IsNullable)
            {
                changes.Add(new ColumnChange(
                    old.Name,
                    now.IsNullable ? ColumnChangeKind.BecameNullable : ColumnChangeKind.BecameRequired));
            }
        }

        foreach (var added in incoming.Where(c => !currentByName.ContainsKey(c.Name)))
            changes.Add(new ColumnChange(added.Name, ColumnChangeKind.Added, To: added.Type));

        return new SchemaComparison(Judge(changes), changes);
    }

    /// <summary>
    /// Só perda de informação é quebra: coluna que sumiu e tipo que estreitou.
    /// Coluna nova, tipo alargado e mudança de nulidade seguem em frente.
    /// </summary>
    private static SchemaVerdict Judge(List<ColumnChange> changes)
    {
        if (changes.Count == 0)
            return SchemaVerdict.Identical;

        var breaking = changes.Any(c =>
            c.Kind is ColumnChangeKind.Removed or ColumnChangeKind.TypeNarrowed);

        return breaking ? SchemaVerdict.Breaking : SchemaVerdict.Compatible;
    }
}
