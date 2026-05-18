using ObservabilityLab.Alerting.Domain;
using ObservabilityLab.Observability.Dashboard;

namespace ObservabilityLab.Alerting.Abstractions;

/// <summary>
/// Contrato de política de alertas — Strategy Pattern.
/// Recebe snapshot do estado do sistema e retorna lista de alertas disparados.
/// </summary>
public interface IAlertPolicy
{
    string Name { get; }
    IReadOnlyList<Alert> Evaluate(DashboardSnapshot snapshot);
}
