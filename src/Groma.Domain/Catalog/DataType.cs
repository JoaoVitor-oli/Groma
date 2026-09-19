namespace Groma.Domain.Catalog;

/// <summary>
/// Tipos que o Groma reconhece numa coluna. Deliberadamente poucos: o catálogo
/// precisa descrever dados vindos de CSV, Excel, SQL e JSON com o mesmo vocabulário.
/// </summary>
public enum DataType
{
    Unknown = 0,
    Boolean,
    Integer,
    Decimal,
    Date,
    Timestamp,
    Text
}

public static class DataTypeRules
{
    /// <summary>
    /// Para cada tipo, os tipos que comportam todos os seus valores sem perda.
    /// </summary>
    private static readonly Dictionary<DataType, DataType[]> Wider = new()
    {
        [DataType.Unknown] =
        [
            DataType.Boolean, DataType.Integer, DataType.Decimal,
            DataType.Date, DataType.Timestamp, DataType.Text
        ],
        [DataType.Boolean] = [DataType.Text],
        [DataType.Integer] = [DataType.Decimal, DataType.Text],
        [DataType.Decimal] = [DataType.Text],
        [DataType.Date] = [DataType.Timestamp, DataType.Text],
        [DataType.Timestamp] = [DataType.Text],
        [DataType.Text] = []
    };

    /// <summary>
    /// Verdadeiro quando mudar de <paramref name="from"/> para <paramref name="to"/>
    /// não descarta informação. Integer vira Decimal sem perda; o contrário, não.
    /// </summary>
    public static bool CanWidenTo(this DataType from, DataType to)
        => from == to || Wider[from].Contains(to);
}
