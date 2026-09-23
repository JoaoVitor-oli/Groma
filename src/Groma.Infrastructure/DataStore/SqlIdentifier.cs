using System.Text;
using Groma.Domain.Exceptions;

namespace Groma.Infrastructure.DataStore;

/// <summary>
/// Um identificador SQL validado e pronto para ser concatenado numa query.
/// </summary>
/// <remarks>
/// Este é o único lugar do Groma onde texto vindo de fora — nome de coluna de um
/// CSV de terceiro — se transforma em SQL. Identificador não pode ser parâmetro
/// de comando, então a concatenação é inevitável; o que dá para fazer é obrigar
/// toda concatenação a passar por aqui, e cobrir este arquivo de testes.
/// <para>
/// Nada é "sanitizado" por remoção de caracteres: a estratégia é validar o que é
/// inaceitável e citar o resto. Aspas internas viram aspas duplicadas, que é como
/// o Postgres escapa. Assim <c>"; DROP TABLE users; --</c> vira um nome de coluna
/// esquisito, não um comando.
/// </para>
/// </remarks>
public sealed record SqlIdentifier
{
    /// <summary>
    /// Limite do Postgres (NAMEDATALEN - 1). Acima disso ele trunca em silêncio,
    /// o que faria duas colunas longas virarem a mesma no banco sem ninguém notar.
    /// </summary>
    public const int MaxBytes = 63;

    private SqlIdentifier(string value)
    {
        Value = value;
        Quoted = $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    /// <summary>Nome físico, já dobrado para minúsculas. Sem aspas.</summary>
    public string Value { get; }

    /// <summary>Pronto para entrar na query, entre aspas e com aspas internas escapadas.</summary>
    public string Quoted { get; }

    /// <summary>
    /// Valida e dobra para minúsculas.
    /// </summary>
    /// <remarks>
    /// O dobramento existe porque o domínio trata nomes de coluna sem diferenciar
    /// maiúsculas, mas identificador citado no Postgres é sensível a elas. Sem
    /// dobrar, uma carga com <c>CNPJ</c> e a seguinte com <c>cnpj</c> seriam o
    /// mesmo schema para o domínio e duas colunas diferentes para o banco.
    /// O nome como a pessoa vê continua guardado no catálogo.
    /// </remarks>
    public static SqlIdentifier For(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Identificador SQL não pode ser vazio.");

        var trimmed = name.Trim();

        foreach (var c in trimmed)
        {
            if (c == '\0')
                throw new DomainException("Identificador SQL não pode conter byte nulo.");

            if (char.IsControl(c))
                throw new DomainException(
                    $"Identificador SQL '{Describe(trimmed)}' contém caractere de controle.");
        }

        var folded = trimmed.ToLowerInvariant();
        var bytes = Encoding.UTF8.GetByteCount(folded);

        if (bytes > MaxBytes)
            throw new DomainException(
                $"Identificador '{Describe(folded)}' tem {bytes} bytes; o Postgres aceita no máximo {MaxBytes}.");

        return new SqlIdentifier(folded);
    }

    /// <summary>Monta <c>"schema"."tabela"</c>.</summary>
    public static string Qualify(SqlIdentifier schema, SqlIdentifier table)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(table);

        return $"{schema.Quoted}.{table.Quoted}";
    }

    public override string ToString() => Quoted;

    /// <summary>Encurta o nome na mensagem de erro, para não vazar um arquivo inteiro no log.</summary>
    private static string Describe(string name)
        => name.Length <= 40 ? name : string.Concat(name.AsSpan(0, 40), "…");
}
