namespace Groma.Application.Abstractions;

/// <summary>
/// O tempo como dependência. Existe para que um teste possa dizer que "hoje" é
/// 18 de setembro de 2026 sem depender do relógio da máquina.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
