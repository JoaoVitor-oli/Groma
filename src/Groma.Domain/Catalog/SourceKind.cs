namespace Groma.Domain.Catalog;

/// <summary>
/// De onde os dados vêm. Cada tipo tem um leitor próprio na infraestrutura,
/// mas todos entram no catálogo pelo mesmo caminho.
/// </summary>
public enum SourceKind
{
    /// <summary>Arquivo enviado pela interface: CSV ou Excel.</summary>
    Upload = 0,

    /// <summary>Banco relacional externo, lido por consulta.</summary>
    SqlDatabase,

    /// <summary>API REST paginada.</summary>
    RestApi
}
