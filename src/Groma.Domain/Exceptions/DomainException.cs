namespace Groma.Domain.Exceptions;

/// <summary>
/// Violação de uma regra de negócio. Traduzida em HTTP 400 pela camada de API.
/// </summary>
public class DomainException(string message) : Exception(message);
