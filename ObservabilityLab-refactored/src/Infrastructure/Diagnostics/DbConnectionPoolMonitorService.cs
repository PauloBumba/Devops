using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Infrastructure.Diagnostics;

/// <summary>
/// Background service que monitora o connection pool do Npgsql a cada 10s.
/// Alerta se demorar mais de 100ms para adquirir uma conexão (pool saturado).
/// </summary>
public sealed class DbConnectionPoolMonitorService(
    IConfiguration config,
    AppMetrics     metrics,
    ILogger<DbConnectionPoolMonitorService> logger) : BackgroundService
{
    private readonly string _connectionString =
        config.GetConnectionString("DefaultConnection") ?? string.Empty;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("{Service} started", nameof(DbConnectionPoolMonitorService));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                await MonitorPoolAsync();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Connection pool monitor error");
            }
        }
    }

    private async Task MonitorPoolAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            sw.Stop();

            if (sw.ElapsedMilliseconds > 100)
                logger.LogWarning(
                    "DB connection took {Ms}ms to acquire — pool may be saturated",
                    sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to acquire DB connection in {Ms}ms", sw.ElapsedMilliseconds);
        }
    }
}
