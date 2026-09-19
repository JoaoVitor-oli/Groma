using Groma.Domain.Catalog;

namespace Groma.Application.Abstractions.Ingestion;

/// <summary>
/// Uma linha como saiu da fonte: valores de texto, na ordem do cabeçalho, sem
/// conversão de tipo. A conversão acontece na carga, depois que o schema é conhecido.
/// Nulo significa ausente; string vazia significa vazia — são coisas diferentes.
/// </summary>
public sealed record RawRow(IReadOnlyList<string?> Values);

/// <summary>
/// Leitura de uma fonte em streaming. Uma implementação por formato: CSV, Excel,
/// consulta SQL, resposta de API.
/// </summary>
public interface ISourceReader : IAsyncDisposable
{
    /// <summary>Nomes das colunas, na ordem em que aparecem na fonte.</summary>
    Task<IReadOnlyList<string>> ReadHeaderAsync(CancellationToken cancellationToken);

    /// <summary>
    /// As linhas, uma a uma. Deve ser preguiçoso de verdade: um arquivo de 2 GB
    /// não pode virar 2 GB de memória.
    /// </summary>
    IAsyncEnumerable<RawRow> ReadRowsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Escolhe o leitor certo para a fonte. O <paramref name="payloadRef"/> é o
/// ponteiro para o conteúdo desta carga — caminho do arquivo enviado, no caso
/// de upload; ignorado pelas fontes que sabem se conectar sozinhas.
/// </summary>
public interface ISourceReaderFactory
{
    ISourceReader Open(Source source, string payloadRef);
}
