using Groma.Domain.Catalog;
using Groma.Domain.Exceptions;

namespace Groma.Domain.Tests.Catalog;

public class ConnectionSettingsTests
{
    [Fact]
    public void Upload_nao_pede_credencial()
    {
        var conn = ConnectionSettings.ForUpload();

        Assert.Equal(SourceKind.Upload, conn.Kind);
        Assert.False(conn.RequiresSecret);
        Assert.Null(conn.Secret);
    }

    [Fact]
    public void Banco_guarda_a_referencia_e_nunca_a_senha()
    {
        var conn = ConnectionSettings.ForSqlDatabase(
            "db.empresa.com", "vendas", "groma_ro", SecretRef.From("fonte-vendas-senha"));

        Assert.Equal(SourceKind.SqlDatabase, conn.Kind);
        Assert.Equal("fonte-vendas-senha", conn.Secret!.Key);
        Assert.Equal("groma_ro", conn.Username);
    }

    [Fact]
    public void Banco_sem_host_e_recusado()
    {
        var ex = Assert.Throws<DomainException>(() =>
            ConnectionSettings.ForSqlDatabase(" ", "vendas", "groma_ro", SecretRef.From("k")));

        Assert.Contains("Host do banco", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_aceita_url_valida_e_dispensa_segredo()
    {
        var conn = ConnectionSettings.ForRestApi("https://api.empresa.com/v1");

        Assert.Equal(SourceKind.RestApi, conn.Kind);
        Assert.False(conn.RequiresSecret);
    }

    [Theory]
    [InlineData("api.empresa.com")]
    [InlineData("ftp://api.empresa.com")]
    [InlineData("")]
    public void Api_com_url_invalida_e_recusada(string url)
        => Assert.Throws<DomainException>(() => ConnectionSettings.ForRestApi(url));

    [Fact]
    public void Referencia_de_segredo_vazia_e_recusada()
        => Assert.Throws<DomainException>(() => SecretRef.From("   "));
}

public class SourceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 18, 7, 12, 0, TimeSpan.Zero);

    private static Source NovaFonte(string nome = "Planilha de vendas")
        => Source.Create(nome, ConnectionSettings.ForUpload(), Agora);

    [Fact]
    public void Fonte_nasce_ativa_e_sem_datasets()
    {
        var fonte = NovaFonte();

        Assert.True(fonte.IsActive);
        Assert.Empty(fonte.Datasets);
        Assert.Equal(SourceKind.Upload, fonte.Kind);
    }

    [Fact]
    public void Fonte_sem_nome_e_recusada()
        => Assert.Throws<DomainException>(() =>
            Source.Create("  ", ConnectionSettings.ForUpload(), Agora));

    [Fact]
    public void Upload_costuma_ter_um_dataset_so()
    {
        var fonte = NovaFonte();

        var dataset = fonte.AddDataset("vendas_2026_q1", Agora);

        Assert.Single(fonte.Datasets);
        Assert.Equal(fonte.Id, dataset.SourceId);
        Assert.True(dataset.NeverIngested);
    }

    [Fact]
    public void Banco_pode_ter_varios_datasets()
    {
        var fonte = Source.Create(
            "ERP produção",
            ConnectionSettings.ForSqlDatabase("db.empresa.com", "erp", "groma_ro", SecretRef.From("erp-senha")),
            Agora);

        fonte.AddDataset("pedidos", Agora);
        fonte.AddDataset("clientes", Agora);
        fonte.AddDataset("produtos", Agora);

        Assert.Equal(3, fonte.Datasets.Count);
    }

    [Fact]
    public void Dataset_repetido_na_mesma_fonte_e_recusado()
    {
        var fonte = NovaFonte();
        fonte.AddDataset("vendas", Agora);

        var ex = Assert.Throws<DomainException>(() => fonte.AddDataset("VENDAS", Agora));

        Assert.Contains("já tem um dataset", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fonte_inativa_nao_aceita_dataset_novo()
    {
        var fonte = NovaFonte();
        fonte.Deactivate();

        Assert.Throws<DomainException>(() => fonte.AddDataset("vendas", Agora));
    }

    [Fact]
    public void Busca_de_dataset_ignora_maiusculas()
    {
        var fonte = NovaFonte();
        fonte.AddDataset("vendas", Agora);

        Assert.NotNull(fonte.FindDataset("VENDAS"));
        Assert.Null(fonte.FindDataset("compras"));
    }

    [Fact]
    public void Rotacionar_credencial_mantem_o_tipo()
    {
        var fonte = Source.Create(
            "ERP",
            ConnectionSettings.ForSqlDatabase("db1", "erp", "groma_ro", SecretRef.From("v1")),
            Agora);

        fonte.UpdateConnection(
            ConnectionSettings.ForSqlDatabase("db2", "erp", "groma_ro", SecretRef.From("v2")));

        Assert.Equal("db2", fonte.Connection.Endpoint);
        Assert.Equal("v2", fonte.Connection.Secret!.Key);
    }

    [Fact]
    public void Banco_nao_vira_api()
    {
        var fonte = Source.Create(
            "ERP",
            ConnectionSettings.ForSqlDatabase("db1", "erp", "groma_ro", SecretRef.From("v1")),
            Agora);

        var ex = Assert.Throws<DomainException>(() =>
            fonte.UpdateConnection(ConnectionSettings.ForRestApi("https://api.empresa.com")));

        Assert.Contains("não pode virar", ex.Message, StringComparison.Ordinal);
    }
}

public class DatasetTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 18, 7, 12, 0, TimeSpan.Zero);

    private static Dataset NovoDataset()
        => Source.Create("Planilha", ConnectionSettings.ForUpload(), Agora)
                 .AddDataset("vendas", Agora);

    [Fact]
    public void Primeira_carga_registra_versao_e_data()
    {
        var dataset = NovoDataset();

        dataset.RecordIngestion(1, Agora);

        Assert.Equal(1, dataset.CurrentVersionNumber);
        Assert.Equal(Agora, dataset.LastIngestedAt);
        Assert.False(dataset.NeverIngested);
    }

    [Fact]
    public void Carga_sem_mudanca_de_schema_repete_a_versao()
    {
        var dataset = NovoDataset();
        dataset.RecordIngestion(4, Agora);

        dataset.RecordIngestion(4, Agora.AddDays(1));

        Assert.Equal(4, dataset.CurrentVersionNumber);
        Assert.Equal(Agora.AddDays(1), dataset.LastIngestedAt);
    }

    [Fact]
    public void Versao_nao_anda_para_tras()
    {
        var dataset = NovoDataset();
        dataset.RecordIngestion(4, Agora);

        var ex = Assert.Throws<DomainException>(() => dataset.RecordIngestion(3, Agora.AddDays(1)));

        Assert.Contains("não pode voltar", ex.Message, StringComparison.Ordinal);
    }
}
