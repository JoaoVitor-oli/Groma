using Groma.Application.Abstractions.Persistence;
using Groma.Domain.Catalog;
using Groma.Domain.Exceptions;

namespace Groma.Infrastructure.DataStore;

/// <summary>
/// Gera o DDL das tabelas de dados ingeridos.
/// </summary>
/// <remarks>
/// Só monta strings — não abre conexão nem executa nada. Quem executa é o
/// <c>DapperDataStore</c>. Essa separação é o que permite testar a geração de SQL
/// sem subir banco, e é onde mora a maior parte do risco.
/// </remarks>
public sealed class DynamicTableBuilder
{
    /// <summary>
    /// Mapa de tipo do catálogo para tipo do Postgres.
    /// </summary>
    /// <remarks>
    /// <c>Integer</c> vira <c>bigint</c> e não <c>integer</c> de propósito: a
    /// inferência olha uma amostra, e um <c>int</c> estourado na linha 200 mil
    /// quebraria a carga inteira por uma economia de 4 bytes por linha.
    /// <c>Decimal</c> vira <c>numeric</c> sem precisão declarada, que no Postgres
    /// aceita qualquer escala — precisão fixa erraria com dado de origem desconhecida.
    /// </remarks>
    public static string PostgresTypeFor(DataType type) => type switch
    {
        DataType.Boolean => "boolean",
        DataType.Integer => "bigint",
        DataType.Decimal => "numeric",
        DataType.Date => "date",
        DataType.Timestamp => "timestamptz",
        DataType.Text => "text",
        DataType.Unknown => "text",
        _ => throw new DomainException($"Tipo {type} não tem equivalente no Postgres.")
    };

    public string CreateSchema(string schema)
    {
        var id = SqlIdentifier.For(schema);
        return $"CREATE SCHEMA IF NOT EXISTS {id.Quoted};";
    }

    /// <summary>
    /// <c>CREATE TABLE IF NOT EXISTS</c> com uma coluna por definição do schema.
    /// </summary>
    /// <remarks>
    /// Nenhuma coluna nasce <c>NOT NULL</c>, mesmo quando a inferência achou que
    /// a coluna é obrigatória. A amostra são as primeiras 500 linhas; declarar a
    /// restrição no banco faria a carga morrer no primeiro nulo da linha 90 mil.
    /// Obrigatoriedade é assunto das regras de qualidade, que reportam em vez de
    /// derrubar a ingestão.
    /// </remarks>
    public string CreateTable(TableSpec table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var qualified = Qualified(table);
        var columns = table.Columns
            .OrderBy(c => c.Ordinal)
            .Select(c => $"  {SqlIdentifier.For(c.Name).Quoted} {PostgresTypeFor(c.Type)}");

        return $"""
                CREATE TABLE IF NOT EXISTS {qualified} (
                {string.Join(",\n", columns)}
                );
                """;
    }

    /// <summary>
    /// Um <c>ALTER TABLE ADD COLUMN</c> para cada coluna do schema que ainda não
    /// existe fisicamente. Nunca gera <c>DROP COLUMN</c>: quem decide sobre perda
    /// de dado é o domínio, e ele já barrou a carga antes de chegar aqui.
    /// </summary>
    public IReadOnlyList<string> AddMissingColumns(
        TableSpec table,
        IReadOnlyCollection<string> existingColumns)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(existingColumns);

        var qualified = Qualified(table);

        // Comparação no nome físico já dobrado, não no nome que veio do arquivo.
        var existing = existingColumns
            .Select(c => SqlIdentifier.For(c).Value)
            .ToHashSet(StringComparer.Ordinal);

        return
        [
            .. table.Columns
                .OrderBy(c => c.Ordinal)
                .Select(c => (Column: c, Id: SqlIdentifier.For(c.Name)))
                .Where(x => !existing.Contains(x.Id.Value))
                .Select(x =>
                    $"ALTER TABLE {qualified} ADD COLUMN {x.Id.Quoted} {PostgresTypeFor(x.Column.Type)};")
        ];
    }

    public string TruncateTable(TableSpec table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return $"TRUNCATE TABLE {Qualified(table)};";
    }

    private static string Qualified(TableSpec table)
        => SqlIdentifier.Qualify(
            SqlIdentifier.For(table.Schema),
            SqlIdentifier.For(table.TableName));
}
