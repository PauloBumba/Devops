using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Api.BackgroundServices;

/// <summary>
/// Coleta CPU/Memória/GC a cada 5 segundos e atualiza DashboardState + AppMetrics.
/// Responsabilidade única: coleta periódica de métricas do processo.
/// </summary>
public sealed class SystemMetricsCollectorService(
    DashboardState                          dashboard,
    AppMetrics                              metrics,
    ILogger<SystemMetricsCollectorService>  logger) : BackgroundService
{
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _prevCpuTime     = TimeSpan.Zero;
    private DateTime _prevSampleTime  = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("{Service} started", nameof(SystemMetricsCollectorService));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                CollectAndPublish();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error collecting system metrics");
            }
        }
    }

    private void CollectAndPublish()
    {
        _process.Refresh();

        var now        = DateTime.UtcNow;
        var cpuTime    = _process.TotalProcessorTime;
        var wallTime   = (now - _prevSampleTime).TotalMilliseconds;
        var cpuDelta   = (cpuTime - _prevCpuTime).TotalMilliseconds;
        var cpuPercent = wallTime > 0
            ? Math.Round(cpuDelta / (wallTime * Environment.ProcessorCount) * 100, 1)
            : 0;

        _prevCpuTime    = cpuTime;
        _prevSampleTime = now;

        var workingSetMb = _process.WorkingSet64 / 1024.0 / 1024.0;
        var totalRamMb   = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024.0 / 1024.0;
        var memPercent   = totalRamMb > 0
            ? Math.Round(workingSetMb / totalRamMb * 100, 1)
            : 0;

        dashboard.UpdateSystemMetrics(cpuPercent, workingSetMb, memPercent);
        metrics.UpdateSystemMetrics(cpuPercent, workingSetMb);

        logger.LogDebug(
            "Metrics — CPU: {Cpu:F1}% | Memory: {Mem:F0}MB ({MemPct:F1}%) | Gen0/1/2: {G0}/{G1}/{G2}",
            cpuPercent, workingSetMb, memPercent,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    }

    public override void Dispose()
    {
        _process.Dispose();
        base.Dispose();
    }
}
