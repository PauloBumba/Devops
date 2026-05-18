namespace ObservabilityLab.Application.Abstractions;

/// <summary>
/// Abstrai o relógio do sistema para permitir testes determinísticos.
/// Implementado em Infrastructure com DateTimeOffset.UtcNow.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
