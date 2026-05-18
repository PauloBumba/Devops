namespace ObservabilityLab.Domain.Enums;

/// <summary>
/// Nível de severidade de um alerta.
/// Movido para Domain pois é um conceito de negócio compartilhado.
/// </summary>
public enum AlertSeverity
{
    Info     = 0,
    Warning  = 1,
    Critical = 2
}
