using Groma.Application.Abstractions.Ingestion;
using Groma.Application.Ingestion.Pipeline;
using Groma.Application.Ingestion.StartIngestion;
using Groma.Application.Tests.Fakes;
using Groma.Domain.Catalog;
using Groma.Domain.Exceptions;

namespace Groma.Application.Tests.Ingestion;

public class IngestionPipelineTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 18, 7, 12, 0, TimeSpan.Zero);

    private static ColumnDefinition Col(string nome, DataType tipo, int ordinal)
        => new(nome, tipo, true, ordinal);

    private static readonly List<ColumnDefinition> SchemaBase =
    [
        Col("cnpj", DataType.Text, 0),
        Col("valor", DataType.Decimal, 1)
    ];

    private static List<RawRow> Linhas(int quantidade)
        => [.. Enumerable.Range(1, quantidade)
            .Select(i => new RawRow([$"cnpj-{i}", i.ToString(System.Globalization.CultureInfo.InvariantCulture)]))];

    private sealed record Cenario(
        StartIngestionHandler Handler,
        FakeCatalogRepository Catalog,
        FakeDataStore Store,
        FakeSourceReader Reader,
        Source Source,
        FakeSchemaInferrer Inferrer);

    private static Cenario Montar(
        IReadOnlyList<ColumnDefinition> schemaInferido,
        int quantidadeLinhas = 10)
    {
        var clock = new FixedClock(Agora);
        var catalog = new FakeCatalogRepository();
        var store = new FakeDataStore();
        var reader = new FakeSourceReader(["cnpj", "valor"], Linhas(quantidadeLinhas));
        var inferrer = new FakeSchemaInferrer(schemaInferido);

        var source = Source.Create("Planilha de vendas", ConnectionSettings.ForUpload(), Agora);
        catalog.Seed(source);

        var pipeline = new IngestionPipeline(
        [
            new ReadStage(reader),
            new InferSchemaStage(inferrer),
            new ResolveVersionStage(catalog, clock),
            new PersistStage(store, clock)
        ]);

        var handler = new StartIngestionHandler(catalog, new FakeUnitOfWork(catalog), pipeline, clock);

        return new Cenario(handler, catalog, store, reader, source, inferrer);
    }

    [Fact]
    public async Task Primeira_carga_cria_dataset_versao_um_e_grava_as_linhas()
    {
        var c = Montar(SchemaBase, quantidadeLinhas: 10);

        var outcome = await c.Handler.HandleAsync(
            new StartIngestionCommand(c.Source.Id, "vendas_2026_q1"), CancellationToken.None);

        Assert.Equal(SchemaVerdict.Compatible, outcome.Verdict);
        Assert.Equal(1, outcome.VersionNumber);
        Assert.Equal(10, outcome.RowsLoaded);
        Assert.False(outcome.Blocked);

        var dataset = Assert.Single(c.Source.Datasets);
        Assert.Equal(1, dataset.CurrentVersionNumber);
        Assert.Equal($"ds_{dataset.Id:N}", c.Store.LastTable!.TableName);
        Assert.Equal("data", c.Store.LastTable.Schema);
    }

    [Fact]
    public async Task Fonte_e_lida_uma_vez_so_e_a_amostra_entra_na_carga()
    {
        // Menos linhas que o tamanho da amostra: tudo passa pela amostra e
        // precisa aparecer na carga mesmo assim.
        var c = Montar(SchemaBase, quantidadeLinhas: 7);

        var outcome = await c.Handler.HandleAsync(
            new StartIngestionCommand(c.Source.Id, "vendas"), CancellationToken.None);

        Assert.Equal(1, c.Reader.OpenCount);
        Assert.Equal(7, outcome.RowsLoaded);
        Assert.Equal(7, c.Store.Written.Count);
        Assert.Equal("cnpj-1", c.Store.Written[0].Values[0]);
        Assert.True(c.Reader.Disposed);
    }

    [Fact]
    public async Task Amostra_para_no_limite_mas_a_carga_leva_tudo()
    {
        var total = ReadStage.SampleSize + 120;
        var c = Montar(SchemaBase, quantidadeLinhas: total);

        var outcome = await c.Handler.HandleAsync(
            new StartIngestionCommand(c.Source.Id, "vendas"), CancellationToken.None);

        Assert.Equal(ReadStage.SampleSize, c.Inferrer.SampleSeen!.Count);
        Assert.Equal(total, outcome.RowsLoaded);
        Assert.Equal(1, c.Reader.OpenCount);
    }

    [Fact]
    public async Task Segunda_carga_sem_mudanca_mantem_a_versao()
    {
        var c = Montar(SchemaBase);
        var cmd = new StartIngestionCommand(c.Source.Id, "vendas");

        await c.Handler.HandleAsync(cmd, CancellationToken.None);
        var outcome = await c.Handler.HandleAsync(cmd, CancellationToken.None);

        Assert.Equal(SchemaVerdict.Identical, outcome.Verdict);
        Assert.Equal(1, outcome.VersionNumber);
        Assert.Single(c.Catalog.Versions);
        Assert.Single(c.Source.Datasets);
    }

    [Fact]
    public async Task Coluna_nova_gera_a_versao_seguinte()
    {
        var c = Montar(SchemaBase);
        var cmd = new StartIngestionCommand(c.Source.Id, "vendas");
        await c.Handler.HandleAsync(cmd, CancellationToken.None);

        // A carga seguinte traz uma coluna a mais.
        var ampliado = new List<ColumnDefinition>(SchemaBase) { Col("regiao", DataType.Text, 2) };
        var c2 = RemontarCom(c, ampliado);

        var outcome = await c2.HandleAsync(cmd, CancellationToken.None);

        Assert.Equal(SchemaVerdict.Compatible, outcome.Verdict);
        Assert.Equal(2, outcome.VersionNumber);
        Assert.Equal(2, c.Catalog.Versions.Count);
    }

    [Fact]
    public async Task Coluna_removida_barra_a_carga_e_nao_grava_nada()
    {
        var c = Montar(SchemaBase);
        var cmd = new StartIngestionCommand(c.Source.Id, "vendas");
        await c.Handler.HandleAsync(cmd, CancellationToken.None);

        var reduzido = new List<ColumnDefinition> { Col("cnpj", DataType.Text, 0) };
        var c2 = RemontarCom(c, reduzido);

        c.Store.Written.Clear();
        var outcome = await c2.HandleAsync(cmd, CancellationToken.None);

        Assert.True(outcome.Blocked);
        Assert.Equal(SchemaVerdict.Breaking, outcome.Verdict);
        Assert.Empty(c.Store.Written);
        Assert.Single(c.Catalog.Versions);
        Assert.Equal(ColumnChangeKind.Removed, Assert.Single(outcome.Changes).Kind);
    }

    [Fact]
    public async Task Fonte_inexistente_estoura()
    {
        var c = Montar(SchemaBase);

        await Assert.ThrowsAsync<DomainException>(() =>
            c.Handler.HandleAsync(
                new StartIngestionCommand(Guid.CreateVersion7(), "vendas"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Fonte_inativa_estoura()
    {
        var c = Montar(SchemaBase);
        c.Source.Deactivate();

        await Assert.ThrowsAsync<DomainException>(() =>
            c.Handler.HandleAsync(
                new StartIngestionCommand(c.Source.Id, "vendas"),
                CancellationToken.None));
    }

    /// <summary>
    /// Monta um handler novo sobre o mesmo catÃ¡logo e a mesma fonte, mudando sÃ³
    /// o schema que a inferÃªncia vai devolver â€” simula a carga seguinte com o
    /// arquivo diferente.
    /// </summary>
    private static StartIngestionHandler RemontarCom(Cenario c, IReadOnlyList<ColumnDefinition> novoSchema)
    {
        var clock = new FixedClock(Agora.AddDays(1));
        var reader = new FakeSourceReader(["cnpj", "valor"], Linhas(10));

        var pipeline = new IngestionPipeline(
        [
            new ReadStage(reader),
            new InferSchemaStage(new FakeSchemaInferrer(novoSchema)),
            new ResolveVersionStage(c.Catalog, clock),
            new PersistStage(c.Store, clock)
        ]);

        return new StartIngestionHandler(c.Catalog, new FakeUnitOfWork(c.Catalog), pipeline, clock);
    }
}

