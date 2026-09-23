using Groma.Application.Abstractions.Persistence;
using Groma.Domain.Catalog;
using Groma.Infrastructure.DataStore;

namespace Groma.IntegrationTests.DataStore;

public class DynamicTableBuilderTests
{
    private readonly DynamicTableBuilder _builder = new();

    private static ColumnDefinition Col(string nome, DataType tipo, int ordinal)
        => new(nome, tipo, true, ordinal);

    private static TableSpec Tabela(params ColumnDefinition[] colunas)
        => new("data", "ds_0193f2a1", colunas);

    [Fact]
    public void Create_table_usa_if_not_exists_e_nome_qualificado()
    {
        var sql = _builder.CreateTable(Tabela(Col("cnpj", DataType.Text, 0)));

        Assert.Contains("CREATE TABLE IF NOT EXISTS \"data\".\"ds_0193f2a1\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Colunas_saem_na_ordem_do_ordinal()
    {
        var sql = _builder.CreateTable(Tabela(
            Col("terceira", DataType.Text, 2),
            Col("primeira", DataType.Text, 0),
            Col("segunda", DataType.Text, 1)));

        var posPrimeira = sql.IndexOf("\"primeira\"", StringComparison.Ordinal);
        var posSegunda = sql.IndexOf("\"segunda\"", StringComparison.Ordinal);
        var posTerceira = sql.IndexOf("\"terceira\"", StringComparison.Ordinal);

        Assert.True(posPrimeira < posSegunda && posSegunda < posTerceira);
    }

    [Theory]
    [InlineData(DataType.Boolean, "boolean")]
    [InlineData(DataType.Integer, "bigint")]
    [InlineData(DataType.Decimal, "numeric")]
    [InlineData(DataType.Date, "date")]
    [InlineData(DataType.Timestamp, "timestamptz")]
    [InlineData(DataType.Text, "text")]
    [InlineData(DataType.Unknown, "text")]
    public void Cada_tipo_do_catalogo_tem_equivalente_no_postgres(DataType tipo, string esperado)
        => Assert.Equal(esperado, DynamicTableBuilder.PostgresTypeFor(tipo));

    [Fact]
    public void Nenhuma_coluna_nasce_not_null()
    {
        var sql = _builder.CreateTable(Tabela(
            Col("cnpj", DataType.Text, 0),
            Col("valor", DataType.Decimal, 1)));

        Assert.DoesNotContain("NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Nome_de_coluna_hostil_sai_citado()
    {
        var sql = _builder.CreateTable(Tabela(Col("x\"; DROP TABLE t; --", DataType.Text, 0)));

        Assert.Contains("\"x\"\"; drop table t; --\" text", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_missing_columns_ignora_o_que_ja_existe()
    {
        var tabela = Tabela(
            Col("cnpj", DataType.Text, 0),
            Col("valor", DataType.Decimal, 1),
            Col("regiao", DataType.Text, 2));

        var sql = _builder.AddMissingColumns(tabela, ["cnpj", "valor"]);

        var unico = Assert.Single(sql);
        Assert.Equal(
            "ALTER TABLE \"data\".\"ds_0193f2a1\" ADD COLUMN \"regiao\" text;",
            unico);
    }

    [Fact]
    public void Add_missing_columns_compara_pelo_nome_fisico_dobrado()
    {
        // A coluna existe no banco como "cnpj"; o schema novo traz "CNPJ".
        var tabela = Tabela(Col("CNPJ", DataType.Text, 0));

        Assert.Empty(_builder.AddMissingColumns(tabela, ["cnpj"]));
    }

    [Fact]
    public void Add_missing_columns_nunca_gera_drop()
    {
        // O banco tem uma coluna a mais que o schema atual.
        var tabela = Tabela(Col("cnpj", DataType.Text, 0));

        var sql = _builder.AddMissingColumns(tabela, ["cnpj", "coluna_antiga"]);

        Assert.Empty(sql);
    }

    [Fact]
    public void Create_schema_e_idempotente()
        => Assert.Equal(
            "CREATE SCHEMA IF NOT EXISTS \"data\";",
            _builder.CreateSchema("data"));

    [Fact]
    public void Truncate_usa_nome_qualificado()
        => Assert.Equal(
            "TRUNCATE TABLE \"data\".\"ds_0193f2a1\";",
            _builder.TruncateTable(Tabela(Col("cnpj", DataType.Text, 0))));
}
