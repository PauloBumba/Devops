using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ObservabilityLab.Infrastructure.Health;

/// <summary>
/// Serializa o relatório de health checks em JSON detalhado (RFC 7807 estendido).
/// Responsabilidade única: formatar a resposta HTTP dos health checks.
/// </summary>
public static class HealthCheckResponseWriter
{
    public static async Task WriteDetailedJson(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";

        var result = new
        {
            status    = report.Status.ToString(),
            duration  = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTimeOffset.UtcNow,
            checks    = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration    = e.Value.Duration.TotalMilliseconds,
                tags        = e.Value.Tags,
                data        = e.Value.Data,
                error       = e.Value.Exception?.Message
            })
        };

        await ctx.Response.WriteAsJsonAsync(result);
    }
}
