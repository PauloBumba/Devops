using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Observability;

/// <summary>
/// Registro da camada de Observabilidade: métricas, tracing, dashboard.
/// OpenTelemetry configurado como cross-cutting concern isolado.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddObservability(
        this IServiceCollection          services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        // ── Singletons de estado ──────────────────────────────────────────
        services.AddSingleton<AppMetrics>();
        services.AddSingleton<AppDiagnostics>();
        services.AddSingleton<DashboardState>();

        // ── OpenTelemetry ─────────────────────────────────────────────────
        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(
                    serviceName:    "ObservabilityLab.Api",
                    serviceVersion: "2.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production",
                    ["host.name"]              = Environment.MachineName
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(opts =>
                {
                    opts.RecordException = true;
                    opts.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health")
                                     && !ctx.Request.Path.StartsWithSegments("/metrics");
                })
                .AddEntityFrameworkCoreInstrumentation(opts =>
                {
                    opts.SetDbStatementForText = true;
                    opts.SetDbStatementForStoredProcedure = true;
                })
                .AddRedisInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(AppDiagnostics.ActivitySourceName)
                .AddOtlpExporter(opts =>
                    opts.Endpoint = new Uri(
                        configuration["Otel:Endpoint"] ?? "http://otelcollector:4317"))
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddMeter(AppMetrics.MeterName)
                .AddPrometheusExporter());

        return services;
    }
}
