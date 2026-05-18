using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Observability.Metrics;
using ObservabilityLab.Observability.Dashboard;
namespace ObservabilityLab.Api.Middleware;

/// <summary>
/// Mede duração de cada request, atualiza métricas e detecta requests lentos.
/// Responsabilidade única: timing e emissão de métricas por request.
/// </summary>
public sealed class RequestTimingMiddleware(
    RequestDelegate                    next,
    AppMetrics                         metrics,
    DashboardState                     dashboard,
    ILogger<RequestTimingMiddleware>   logger,
    IConfiguration                     config)
{
    private readonly double _slowThresholdMs =
        config.GetValue<double>("Observability:SlowRequestThresholdMs", 500);

    public async Task InvokeAsync(HttpContext ctx)
    {
        metrics.IncrementActiveRequests();
        var sw = Stopwatch.StartNew();

        try
        {
            await next(ctx);
        }
        finally
        {
            sw.Stop();
            metrics.DecrementActiveRequests();

            var durationMs = sw.Elapsed.TotalMilliseconds;
            var route      = ctx.GetEndpoint()?.DisplayName ?? ctx.Request.Path.Value ?? "unknown";
            var method     = ctx.Request.Method;
            var status     = ctx.Response.StatusCode;

            metrics.RecordRequestCompleted(route, method, status, durationMs);
            dashboard.RecordRequest(route, method, status, durationMs);

            if (durationMs > _slowThresholdMs)
            {
                metrics.SlowRequests.Add(1, new TagList
                {
                    { "http.route",  route },
                    { "http.method", method }
                });

                logger.LogWarning(
                    "SLOW REQUEST — {Method} {Route} | {DurationMs:F1}ms (threshold: {Threshold}ms) | Status: {Status}",
                    method, route, durationMs, _slowThresholdMs, status);

                dashboard.RecordSlowRequest(route, method, durationMs);
            }
        }
    }
}
