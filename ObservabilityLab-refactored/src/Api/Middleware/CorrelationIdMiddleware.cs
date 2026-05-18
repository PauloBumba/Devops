using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ObservabilityLab.Api.Middleware;

/// <summary>
/// Lê ou gera um Correlation-ID por request.
/// Propaga ao header de resposta, ao Serilog e ao span OTel atual.
/// </summary>
public sealed class CorrelationIdMiddleware(
    RequestDelegate                    next,
    ILogger<CorrelationIdMiddleware>   logger)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext ctx)
    {
        var correlationId = ctx.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        ctx.Items["CorrelationId"]       = correlationId;
        ctx.Response.Headers[HeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object>
            { ["CorrelationId"] = correlationId });

        Activity.Current?.SetBaggage("correlation.id", correlationId);
        Activity.Current?.SetTag("correlation.id", correlationId);

        await next(ctx);
    }
}
