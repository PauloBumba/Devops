using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Observability.Dashboard;
using ObservabilityLab.Observability.Metrics;
using ObservabilityLab.Observability.Dashboard;


namespace ObservabilityLab.Api.Middleware;

/// <summary>
/// Captura qualquer exceção não tratada, registra em métricas/tracing
/// e retorna RFC 7807 ProblemDetails padronizado.
/// Responsabilidade única: tratamento global de falhas HTTP.
/// </summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate                     next,
    AppMetrics                          metrics,
    DashboardState                      dashboard,
    ILogger<GlobalExceptionMiddleware>  logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ctx, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext ctx, Exception ex)
    {
        var correlationId = ctx.Items["CorrelationId"]?.ToString() ?? "unknown";
        var traceId       = AppDiagnostics.GetTraceId();

        Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
      

        Activity.Current?.SetTag(
            "exception.type",
            ex.GetType().FullName);

        Activity.Current?.SetTag(
            "exception.message",
            ex.Message);

        Activity.Current?.SetTag(
            "exception.stacktrace",
            ex.StackTrace);

        var (statusCode, title) = ex switch
        {
            ArgumentException or ArgumentNullException => (400, "Bad Request"),
            UnauthorizedAccessException                => (401, "Unauthorized"),
            KeyNotFoundException                       => (404, "Not Found"),
            TimeoutException                           => (408, "Request Timeout"),
            OperationCanceledException                 => (499, "Client Closed Request"),
            _                                         => (500, "Internal Server Error")
        };

        logger.LogError(ex,
            "Unhandled exception — {ExceptionType} | CorrelationId: {CorrelationId} | TraceId: {TraceId}",
            ex.GetType().Name, correlationId, traceId);

        metrics.ErrorCount.Add(1, new TagList
        {
            { "exception.type",   ex.GetType().Name },
            { "http.status_code", statusCode },
            { "http.route",       ctx.Request.Path.Value ?? "/" }
        });

        dashboard.RecordException(ex, ctx.Request.Path.Value ?? "/");

        ctx.Response.StatusCode  = statusCode;
        ctx.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type          = $"https://observabilitylab.dev/errors/{statusCode}",
            title,
            status        = statusCode,
            detail        = ex.Message,
            instance      = ctx.Request.Path.Value,
            correlationId,
            traceId,
            timestamp     = DateTimeOffset.UtcNow
        };

        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}
