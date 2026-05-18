using ObservabilityLab.Observability.Dashboard;
using ObservabilityLab.Observability.Metrics;
using ObservabilityLab.Observability.Dashboard;
namespace ObservabilityLab.Api.Middleware;

/// <summary>
/// Rastreia usuários concorrentes usando contador atômico.
/// Responsabilidade única: contagem de concorrência de requests.
/// </summary>
public sealed class RequestCounterMiddleware(RequestDelegate next, DashboardState dashboard)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        dashboard.IncrementConcurrentUsers();
        try
        {
            await next(ctx);
        }
        finally
        {
            dashboard.DecrementConcurrentUsers();
        }
    }
}
