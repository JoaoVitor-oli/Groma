using Groma.Domain.Exceptions;

namespace Groma.Domain.Catalog;

/// <summary>
/// O "bilhetinho" que diz onde a senha está guardada, nunca a senha em si.
/// O domínio só carrega o nome da chave; quem resolve o valor é a infraestrutura,
/// no momento de abrir a conexão. Assim nenhuma credencial passa por aqui nem
/// chega ao banco do catálogo.
/// </summary>
public sealed record SecretRef
{
    private SecretRef(string key) => Key = key;

    /// <summary>Nome da chave no cofre, por exemplo "fonte-vendas-senha".</summary>
    public string Key { get; }

    public static SecretRef From(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("Referência de segredo não pode ser vazia.");

        var trimmed = key.Trim();

        if (trimmed.Length > 200)
            throw new DomainException("Referência de segredo é longa demais.");

        return new SecretRef(trimmed);
    }

    public override string ToString() => Key;
}
