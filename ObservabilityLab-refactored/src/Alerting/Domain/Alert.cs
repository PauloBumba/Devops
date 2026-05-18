using ObservabilityLab.Domain.Enums;

namespace ObservabilityLab.Alerting.Domain;

/// <summary>
/// Representa um alerta gerado pelo sistema.
/// Imutável por design — records garantem sem mutação acidental.
/// </summary>
public sealed record Alert
{
    public Guid           Id         { get; init; } = Guid.NewGuid();
    public string         Title      { get; init; } = string.Empty;
    public string         Message    { get; init; } = string.Empty;
    public AlertSeverity  Severity   { get; init; }
    public string         Source     { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public Dictionary<string, string> Metadata { get; init; } = [];
}
