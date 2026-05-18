using ObservabilityLab.Application.Abstractions;

namespace ObservabilityLab.Infrastructure.Services;

/// <summary>
/// Implementação real do relógio do sistema.
/// Em testes: substituir por um mock que retorna datas fixas.
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
