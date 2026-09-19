using Groma.Domain.Catalog;

namespace Groma.Application.Abstractions.Ingestion;

/// <summary>
/// Descobre tipos e nulidade olhando uma amostra das primeiras linhas.
/// É palpite informado, não certeza: por isso a comparação de schema trata
/// alargamento de tipo como compatível — a amostra seguinte pode revelar que
/// a coluna que parecia inteira tem decimal na linha 40 mil.
/// </summary>
public interface ISchemaInferrer
{
    IReadOnlyList<ColumnDefinition> Infer(IReadOnlyList<string> header, IReadOnlyList<RawRow> sample);
}
