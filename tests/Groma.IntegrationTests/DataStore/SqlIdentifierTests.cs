using System.Text;
using Groma.Domain.Exceptions;
using Groma.Infrastructure.DataStore;

namespace Groma.IntegrationTests.DataStore;

/// <summary>
/// Testes puros: não sobem banco. Ficam neste projeto por proximidade com o
/// resto do DataStore, mas rodam sem Docker.
/// </summary>
public class SqlIdentifierTests
{
    [Theory]
    [InlineData("cnpj", "\"cnpj\"")]
    [InlineData("valor_total", "\"valor_total\"")]
    [InlineData("CNPJ", "\"cnpj\"")]
    [InlineData("  espacos  ", "\"espacos\"")]
    public void Nome_simples_vira_identificador_citado(string entrada, string esperado)
        => Assert.Equal(esperado, SqlIdentifier.For(entrada).Quoted);

    [Fact]
    public void Nome_com_espaco_no_meio_sobrevive_citado()
        => Assert.Equal("\"valor total\"", SqlIdentifier.For("Valor Total").Quoted);

    [Fact]
    public void Acento_e_preservado()
        => Assert.Equal("\"endereço\"", SqlIdentifier.For("Endereço").Quoted);

    // --- tentativas de injeção -------------------------------------------------

    [Fact]
    public void Ponto_e_virgula_com_comando_vira_nome_de_coluna_esquisito()
    {
        var id = SqlIdentifier.For("x; DROP TABLE users; --");

        Assert.Equal("\"x; drop table users; --\"", id.Quoted);
    }

    [Fact]
    public void Aspas_internas_sao_duplicadas()
    {
        var id = SqlIdentifier.For("fo\"o");

        Assert.Equal("\"fo\"\"o\"", id.Quoted);
    }

    [Fact]
    public void Tentativa_de_fechar_a_citacao_e_neutralizada()
    {
        // O clássico: fechar as aspas e emendar comando.
        var id = SqlIdentifier.For("a\"; DROP TABLE t; --");

        Assert.Equal("\"a\"\"; drop table t; --\"", id.Quoted);
        Assert.StartsWith("\"", id.Quoted, StringComparison.Ordinal);
        Assert.EndsWith("\"", id.Quoted, StringComparison.Ordinal);
    }

    [Fact]
    public void Nome_qualificado_nao_permite_pular_de_schema()
    {
        // Um ponto no nome não vira separador de schema: fica dentro das aspas.
        var id = SqlIdentifier.For("public.usuarios");

        Assert.Equal("\"public.usuarios\"", id.Quoted);
    }

    // --- rejeições -------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Nome_vazio_e_recusado(string entrada)
        => Assert.Throws<DomainException>(() => SqlIdentifier.For(entrada));

    [Fact]
    public void Byte_nulo_e_recusado()
        => Assert.Throws<DomainException>(() => SqlIdentifier.For("co\0luna"));

    [Theory]
    [InlineData("col\nuna")]
    [InlineData("col\runa")]
    public void Caractere_de_controle_e_recusado(string entrada)
        => Assert.Throws<DomainException>(() => SqlIdentifier.For(entrada));

    [Fact]
    public void Nome_no_limite_de_63_bytes_passa()
    {
        var nome = new string('a', 63);

        Assert.Equal(63, Encoding.UTF8.GetByteCount(SqlIdentifier.For(nome).Value));
    }

    [Fact]
    public void Nome_acima_de_63_bytes_e_recusado()
    {
        var nome = new string('a', 64);

        var ex = Assert.Throws<DomainException>(() => SqlIdentifier.For(nome));

        Assert.Contains("64 bytes", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Limite_conta_bytes_e_nao_caracteres()
    {
        // 32 cedilhas = 64 bytes em UTF-8, mesmo sendo 32 caracteres.
        var nome = new string('ç', 32);

        Assert.Equal(32, nome.Length);
        Assert.Throws<DomainException>(() => SqlIdentifier.For(nome));
    }

    [Fact]
    public void Mensagem_de_erro_nao_despeja_nome_gigante()
    {
        var nome = new string('a', 500);

        var ex = Assert.Throws<DomainException>(() => SqlIdentifier.For(nome));

        Assert.True(ex.Message.Length < 150, "a mensagem deve encurtar o nome");
    }

    // --- qualificação ----------------------------------------------------------

    [Fact]
    public void Qualify_monta_schema_ponto_tabela()
    {
        var sql = SqlIdentifier.Qualify(SqlIdentifier.For("data"), SqlIdentifier.For("ds_abc123"));

        Assert.Equal("\"data\".\"ds_abc123\"", sql);
    }

    [Fact]
    public void Identificadores_iguais_sao_iguais()
        => Assert.Equal(SqlIdentifier.For("CNPJ"), SqlIdentifier.For("cnpj"));
}
