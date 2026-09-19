using Groma.Application.Abstractions.Ingestion;
using Groma.Domain.Catalog;

namespace Groma.Application.Abstractions.Persistence;

/// <summary>
/// A tabela física onde os dados de um dataset moram.
/// </summary>
/// <param name="Schema">Schema do Postgres, sempre "data".</param>
/// <param name="TableName">
/// Nome derivado do id do dataset, não do nome que a pessoa deu. Renomear um
/// dataset na interface não deve renomear tabela no banco, e nome vindo de
/// arquivo de terceiro nunca vira identificador SQL.
/// </param>
public sealed record TableSpec(
    string Schema,
    string TableName,
    IReadOnlyList<ColumnDefinition> Columns);

/// <summary>
/// Acesso às tabelas de dados ingeridos — o mundo de schema dinâmico,
/// implementado com Dapper e SQL gerado.
/// </summary>
public interface IDataStore
{
    /// <summary>
    /// Cria a tabela se não existir, ou acrescenta as colunas que faltam.
    /// Nunca remove coluna: quem decide sobre perda de dado é o domínio,
    /// e ele já barrou a carga antes de chegar aqui.
    /// </summary>
    Task EnsureTableAsync(TableSpec table, CancellationToken cancellationToken);

    /// <summary>
    /// Substitui todo o conteúdo da tabela pelas linhas recebidas, em streaming.
    /// Cada carga é um retrato completo da fonte, não um incremento.
    /// </summary>
    /// <returns>Quantidade de linhas gravadas.</returns>
    Task<long> ReplaceAsync(
        TableSpec table,
        IAsyncEnumerable<RawRow> rows,
        CancellationToken cancellationToken);
}
