using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Alerting.Domain;
using ObservabilityLab.Alerting.Services;
using ObservabilityLab.Domain.Enums;
using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Api.BackgroundServices;

/// <summary>
/// Analisa P95 de latência a cada 30 segundos.
/// Emite alertas quando P95 excede 2 segundos nos endpoints críticos.
/// </summary>
public sealed class SlowRequestDetectorService(
    AppMetrics                           metrics,
    AlertService                         alertService,
    ILogger<SlowRequestDetectorService>  logger) : BackgroundService
{
    private static readonly string[] MonitoredRoutes =
    [
        "GET /api/v1/products",
        "POST /api/v1/auth/login",
        "GET /api/v1/lab/slow"
    ];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("{Service} started", nameof(SlowRequestDetectorService));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                await AnalyzeLatencyPatterns(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Service} error", nameof(SlowRequestDetectorService));
            }
        }
    }

    private async Task AnalyzeLatencyPatterns(CancellationToken ct)
    {
        foreach (var route in MonitoredRoutes)
        {
            var percentiles = metrics.GetEndpointPercentiles(route);
            if (percentiles.P95 == 0) continue;

            logger.LogInformation(
                "Latency [{Route}] — P50: {P50:F0}ms | P95: {P95:F0}ms | P99: {P99:F0}ms | Max: {Max:F0}ms",
                route, percentiles.P50, percentiles.P95, percentiles.P99, percentiles.Max);

            if (percentiles.P95 > 2000)
            {
                await alertService.SendWithFallbackAsync(new Alert
                {
                    Title    = $"High P95 Latency — {route}",
                    Message  = $"P95: {percentiles.P95:F0}ms | P99: {percentiles.P99:F0}ms",
                    Severity = AlertSeverity.Warning,
                    Source   = nameof(SlowRequestDetectorService)
                }, ct);
            }
        }
    }
}
