namespace ObservabilityLab.Domain.Enums;

/// <summary>
/// Severidade operacional de alertas e eventos críticos.
/// </summary>
public enum AlertSeverity
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4,
    Emergency = 5
}