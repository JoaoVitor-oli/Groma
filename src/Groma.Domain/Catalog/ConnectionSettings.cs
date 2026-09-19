using Groma.Domain.Exceptions;

namespace Groma.Domain.Catalog;

/// <summary>
/// Como chegar até a fonte. Guarda apenas o que não é sigiloso — endereço, banco,
/// usuário — mais a referência ao segredo. Construída pelos métodos de fábrica,
/// que garantem que cada tipo de fonte receba o que precisa.
/// </summary>
public sealed record ConnectionSettings
{
    private ConnectionSettings(
        SourceKind kind,
        string endpoint,
        string? database,
        string? username,
        SecretRef? secret)
    {
        Kind = kind;
        Endpoint = endpoint;
        Database = database;
        Username = username;
        Secret = secret;
    }

    public SourceKind Kind { get; }

    /// <summary>Host do banco, URL base da API, ou "upload" quando o arquivo vem pela interface.</summary>
    public string Endpoint { get; }

    public string? Database { get; }

    public string? Username { get; }

    /// <summary>Onde a senha está guardada. Nulo quando a fonte não exige credencial.</summary>
    public SecretRef? Secret { get; }

    public bool RequiresSecret => Secret is not null;

    /// <summary>Arquivo enviado pela interface: não há conexão nem credencial.</summary>
    public static ConnectionSettings ForUpload()
        => new(SourceKind.Upload, "upload", null, null, null);

    public static ConnectionSettings ForSqlDatabase(
        string host,
        string database,
        string username,
        SecretRef secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        Require(host, "Host do banco");
        Require(database, "Nome do banco");
        Require(username, "Usuário do banco");

        return new ConnectionSettings(
            SourceKind.SqlDatabase, host.Trim(), database.Trim(), username.Trim(), secret);
    }

    public static ConnectionSettings ForRestApi(string baseUrl, SecretRef? secret = null)
    {
        Require(baseUrl, "URL base da API");

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainException("URL base da API precisa ser um endereço http ou https válido.");
        }

        return new ConnectionSettings(SourceKind.RestApi, uri.ToString(), null, null, secret);
    }

    private static void Require(string value, string campo)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{campo} não pode ser vazio.");
    }
}
