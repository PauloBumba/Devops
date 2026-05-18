using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ObservabilityLab.Infrastructure.Health;

/// <summary>
/// Health check de recursos do sistema: CPU, memória e thread pool.
/// Responsabilidade única: avaliar a saúde dos recursos da máquina.
/// </summary>
public sealed class SystemResourcesHealthCheck : IHealthCheck
{
    private const double CpuWarningThreshold      = 85.0;
    private const double MemoryWarningThresholdMb = 800.0;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  ct = default)
    {
        var proc     = Process.GetCurrentProcess();
        var memoryMb = proc.WorkingSet64 / 1024.0 / 1024.0;

        ThreadPool.GetAvailableThreads(out var availWorker, out _);
        ThreadPool.GetMaxThreads(out var maxWorker, out _);
        var threadUtilPercent = (double)(maxWorker - availWorker) / maxWorker * 100;

        var data = new Dictionary<string, object>
        {
            ["memory_mb"]           = Math.Round(memoryMb, 1),
            ["gc_total_memory_mb"]  = Math.Round(GC.GetTotalMemory(false) / 1024.0 / 1024.0, 1),
            ["thread_pool_used"]    = maxWorker - availWorker,
            ["thread_pool_max"]     = maxWorker,
            ["thread_util_percent"] = Math.Round(threadUtilPercent, 1),
            ["gc_gen0"]             = GC.CollectionCount(0),
            ["gc_gen1"]             = GC.CollectionCount(1),
            ["gc_gen2"]             = GC.CollectionCount(2)
        };

        if (memoryMb > MemoryWarningThresholdMb || threadUtilPercent > 90)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Resources under pressure. Memory: {memoryMb:F0} MB, ThreadPool: {threadUtilPercent:F0}%",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("System resources nominal", data));
    }
}
