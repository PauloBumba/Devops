using Microsoft.Extensions.Configuration;
using ObservabilityLab.Alerting.Abstractions;
using ObservabilityLab.Alerting.Domain;
using ObservabilityLab.Domain.Enums;
using ObservabilityLab.Observability.Dashboard;

namespace ObservabilityLab.Alerting.Policies;

/// <summary>
/// Dispara alertas quando métricas cruzam limiares configurados.
/// Anti-storm: cooldown de 5 min por chave para não spam de alertas iguais.
/// </summary>
public sealed class ThresholdAlertPolicy(IConfiguration config) : IAlertPolicy
{
    public string Name => "ThresholdPolicy";

    private readonly Dictionary<string, DateTimeOffset> _lastFired = [];
    private readonly TimeSpan _cooldown = TimeSpan.FromMinutes(5);

    // Limiares lidos do appsettings — alteráveis sem redeployar
    private double CpuCritical   => config.GetValue<double>("Alerting:Thresholds:CpuCritical",   90);
    private double CpuWarning    => config.GetValue<double>("Alerting:Thresholds:CpuWarning",    75);
    private double MemCritical   => config.GetValue<double>("Alerting:Thresholds:MemCritical",   90);
    private double ErrorRate     => config.GetValue<double>("Alerting:Thresholds:ErrorRate",      5);
    private double AvgLatency    => config.GetValue<double>("Alerting:Thresholds:AvgLatencyMs", 800);
    private long   ConcurrentMax => config.GetValue<long>  ("Alerting:Thresholds:ConcurrentMax", 500);

    public IReadOnlyList<Alert> Evaluate(DashboardSnapshot snap)
    {
        var alerts = new List<Alert>();

        Check(alerts, "cpu-critical", snap.CpuUsagePercent >= CpuCritical, () => new Alert
        {
            Title    = "CPU Critical",
            Message  = $"CPU at {snap.CpuUsagePercent:F1}% (threshold: {CpuCritical}%)",
            Severity = AlertSeverity.Critical,
            Source   = "ThresholdPolicy/CPU",
            Metadata = { ["cpu_percent"] = snap.CpuUsagePercent.ToString("F1") }
        });

        Check(alerts, "cpu-warning", snap.CpuUsagePercent is >= 75 and < 90, () => new Alert
        {
            Title    = "CPU Warning",
            Message  = $"CPU elevated at {snap.CpuUsagePercent:F1}%",
            Severity = AlertSeverity.Warning,
            Source   = "ThresholdPolicy/CPU"
        });

        Check(alerts, "memory-critical", snap.MemoryUsagePercent >= MemCritical, () => new Alert
        {
            Title    = "Memory Critical",
            Message  = $"Memory at {snap.MemoryUsagePercent:F1}% ({snap.MemoryUsageMb:F0} MB)",
            Severity = AlertSeverity.Critical,
            Source   = "ThresholdPolicy/Memory"
        });

        Check(alerts, "error-rate", snap.ErrorRatePercent >= ErrorRate, () => new Alert
        {
            Title    = "High Error Rate",
            Message  = $"Error rate {snap.ErrorRatePercent:F1}% — {snap.TotalErrors} of {snap.TotalRequests} requests",
            Severity = AlertSeverity.Critical,
            Source   = "ThresholdPolicy/ErrorRate",
            Metadata = { ["error_rate"] = snap.ErrorRatePercent.ToString("F1") }
        });

        Check(alerts, "high-latency", snap.AvgResponseMs >= AvgLatency, () => new Alert
        {
            Title    = "High Average Latency",
            Message  = $"Avg response time {snap.AvgResponseMs:F0}ms (threshold: {AvgLatency}ms)",
            Severity = AlertSeverity.Warning,
            Source   = "ThresholdPolicy/Latency"
        });

        Check(alerts, "concurrent-users", snap.ConcurrentUsers >= ConcurrentMax, () => new Alert
        {
            Title    = "Concurrent User Spike",
            Message  = $"{snap.ConcurrentUsers} users active simultaneously (max: {ConcurrentMax})",
            Severity = AlertSeverity.Warning,
            Source   = "ThresholdPolicy/Concurrency"
        });

        return alerts;
    }

    private void Check(List<Alert> alerts, string key, bool condition, Func<Alert> factory)
    {
        if (!condition) return;

        if (_lastFired.TryGetValue(key, out var last)
            && DateTimeOffset.UtcNow - last < _cooldown)
            return;

        _lastFired[key] = DateTimeOffset.UtcNow;
        alerts.Add(factory());
    }
}
