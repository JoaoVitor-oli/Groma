using Groma.Domain.Catalog;
using Groma.Domain.Exceptions;

namespace Groma.Domain.Tests.Catalog;

public class SchemaComparisonTests
{
    private static ColumnDefinition Col(
        string name,
        DataType type = DataType.Text,
        bool nullable = true,
        int ordinal = 0)
        => new(name, type, nullable, ordinal);

    [Fact]
    public void Schemas_iguais_nao_pedem_nova_versao()
    {
        List<ColumnDefinition> atual = [Col("cnpj"), Col("valor", DataType.Decimal, ordinal: 1)];
        List<ColumnDefinition> nova = [Col("cnpj"), Col("valor", DataType.Decimal, ordinal: 1)];

        var result = SchemaComparison.Between(atual, nova);

        Assert.Equal(SchemaVerdict.Identical, result.Verdict);
        Assert.False(result.RequiresNewVersion);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void Coluna_nova_e_compativel()
    {
        List<ColumnDefinition> atual = [Col("cnpj")];
        List<ColumnDefinition> nova = [Col("cnpj"), Col("regiao", ordinal: 1)];

        var result = SchemaComparison.Between(atual, nova);

        Assert.Equal(SchemaVerdict.Compatible, result.Verdict);
        Assert.True(result.CanProceed);
        var change = Assert.Single(result.Changes);
        Assert.Equal(ColumnChangeKind.Added, change.Kind);
        Assert.Equal("regiao", change.ColumnName);
    }

    [Fact]
    public void Coluna_removida_quebra_a_carga()
    {
        List<ColumnDefinition> atual = [Col("cnpj"), Col("regiao", ordinal: 1)];
        List<ColumnDefinition> nova = [Col("cnpj")];

        var result = SchemaComparison.Between(atual, nova);

        Assert.Equal(SchemaVerdict.Breaking, result.Verdict);
        Assert.False(result.CanProceed);
        Assert.Equal(ColumnChangeKind.Removed, Assert.Single(result.Changes).Kind);
    }

    [Theory]
    [InlineData(DataType.Integer, DataType.Decimal)]
    [InlineData(DataType.Integer, DataType.Text)]
    [InlineData(DataType.Date, DataType.Timestamp)]
    [InlineData(DataType.Unknown, DataType.Integer)]
    public void Tipo_alargado_e_compativel(DataType de, DataType para)
    {
        List<ColumnDefinition> atual = [Col("valor", de)];
        List<ColumnDefinition> nova = [Col("valor", para)];

        var result = SchemaComparison.Between(atual, nova);

        Assert.Equal(SchemaVerdict.Compatible, result.Verdict);
        Assert.Equal(ColumnChangeKind.TypeWidened, Assert.Single(result.Changes).Kind);
    }

    [Theory]
    [InlineData(DataType.Decimal, DataType.Integer)]
    [InlineData(DataType.Text, DataType.Date)]
    [InlineData(DataType.Timestamp, DataType.Date)]
    public void Tipo_estreitado_quebra_a_carga(DataType de, DataType para)
    {
        List<ColumnDefinition> atual = [Col("valor", de)];
        List<ColumnDefinition> nova = [Col("valor", para)];

        var result = SchemaComparison.Between(atual, nova);

        Assert.Equal(SchemaVerdict.Breaking, result.Verdict);
        Assert.Equal(ColumnChangeKind.TypeNarrowed, Assert.Single(result.Changes).Kind);
    }

    [Fact]
    public void Nome_de_coluna_ignora_maiusculas()
    {
        List<ColumnDefinition> atual = [Col("CNPJ")];
        List<ColumnDefinition> nova = [Col("cnpj")];

        var result = SchemaComparison.Between(atual, nova);

        Assert.Equal(SchemaVerdict.Identical, result.Verdict);
    }

    [Fact]
    public void Reordenar_coluna_nao_muda_nada()
    {
        List<ColumnDefinition> atual = [Col("cnpj", ordinal: 0), Col("valor", ordinal: 1)];
        List<ColumnDefinition> nova = [Col("valor", ordinal: 0), Col("cnpj", ordinal: 1)];

        var result = SchemaComparison.Between(atual, nova);

        Assert.Equal(SchemaVerdict.Identical, result.Verdict);
    }

    [Fact]
    public void Coluna_que_passou_a_aceitar_nulo_e_compativel()
    {
        List<ColumnDefinition> atual = [Col("regiao", nullable: false)];
        List<ColumnDefinition> nova = [Col("regiao", nullable: true)];

        var result = SchemaComparison.Between(atual, nova);

        Assert.Equal(SchemaVerdict.Compatible, result.Verdict);
        Assert.Equal(ColumnChangeKind.BecameNullable, Assert.Single(result.Changes).Kind);
    }

    [Fact]
    public void Uma_quebra_no_meio_de_mudancas_boas_ainda_e_quebra()
    {
        List<ColumnDefinition> atual = [Col("cnpj"), Col("valor", DataType.Decimal, ordinal: 1)];
        List<ColumnDefinition> nova = [Col("cnpj"), Col("valor", DataType.Integer, ordinal: 1), Col("novo", ordinal: 2)];

        var result = SchemaComparison.Between(atual, nova);

        Assert.Equal(SchemaVerdict.Breaking, result.Verdict);
        Assert.Equal(2, result.Changes.Count);
    }
}

public class DatasetVersionTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 18, 7, 12, 0, TimeSpan.Zero);

    private static DatasetVersion Versao(params string[] colunas)
        => new(
            Guid.CreateVersion7(),
            1,
            colunas.Select((n, i) => new ColumnDefinition(n, DataType.Text, true, i)),
            Agora);

    [Fact]
    public void Versao_sem_coluna_e_recusada()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new DatasetVersion(Guid.CreateVersion7(), 1, [], Agora));

        Assert.Contains("ao menos uma coluna", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Coluna_duplicada_e_recusada()
    {
        var ex = Assert.Throws<DomainException>(() => Versao("cnpj", "CNPJ"));

        Assert.Contains("mais de uma vez", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Numero_de_versao_comeca_em_um()
    {
        Assert.Throws<DomainException>(() =>
            new DatasetVersion(Guid.CreateVersion7(), 0, [new ColumnDefinition("a", DataType.Text, true, 0)], Agora));
    }

    [Fact]
    public void Proxima_versao_incrementa_e_mantem_o_dataset()
    {
        var v1 = Versao("cnpj");

        var v2 = v1.Next([new ColumnDefinition("cnpj", DataType.Text, true, 0)], Agora.AddDays(1));

        Assert.Equal(2, v2.Number);
        Assert.Equal(v1.DatasetId, v2.DatasetId);
        Assert.NotEqual(v1.Id, v2.Id);
    }

    [Fact]
    public void Compare_delega_para_a_comparacao_de_schema()
    {
        var v1 = Versao("cnpj", "valor");

        var result = v1.Compare([new ColumnDefinition("cnpj", DataType.Text, true, 0)]);

        Assert.Equal(SchemaVerdict.Breaking, result.Verdict);
    }
}
