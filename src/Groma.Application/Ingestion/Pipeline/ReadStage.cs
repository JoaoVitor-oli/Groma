using System.Runtime.CompilerServices;
using Groma.Application.Abstractions.Ingestion;

namespace Groma.Application.Ingestion.Pipeline;

/// <summary>
/// Abre a fonte, lê o cabeçalho e guarda as primeiras linhas para a inferência.
/// </summary>
/// <remarks>
/// O detalhe que faz este estágio existir: a inferência precisa de uma amostra,
/// mas a carga precisa de todas as linhas — inclusive as da amostra. Reabrir a
/// fonte para a segunda passada custa caro e nem sempre é possível (uma API
/// paginada pode devolver outra coisa). Então o estágio segura o enumerador,
/// consome as primeiras N linhas para a amostra, e entrega um fluxo que emite
/// a amostra de novo antes de continuar de onde parou.
/// </remarks>
public sealed class ReadStage(ISourceReaderFactory readers) : IIngestionStage
{
    public const int SampleSize = 500;

    public async Task ExecuteAsync(IngestionContext context, CancellationToken cancellationToken)
    {
        var reader = readers.Open(context.Source, context.Request.PayloadRef);

        context.Header = await reader.ReadHeaderAsync(cancellationToken);

        var enumerator = reader.ReadRowsAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

        var sample = new List<RawRow>(SampleSize);

        while (sample.Count < SampleSize && await enumerator.MoveNextAsync())
            sample.Add(enumerator.Current);

        context.Sample = sample;
        context.Rows = Replay(sample, enumerator, reader, cancellationToken);
    }

    private static async IAsyncEnumerable<RawRow> Replay(
        IReadOnlyList<RawRow> sample,
        IAsyncEnumerator<RawRow> rest,
        ISourceReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            foreach (var row in sample)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return row;
            }

            while (await rest.MoveNextAsync())
                yield return rest.Current;
        }
        finally
        {
            await rest.DisposeAsync();
            await reader.DisposeAsync();
        }
    }
}
