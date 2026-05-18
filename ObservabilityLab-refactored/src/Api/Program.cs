using Serilog;
using ObservabilityLab.Api;
using ObservabilityLab.Api.Endpoints.Auth;
using ObservabilityLab.Api.Endpoints.Diagnostic;
using ObservabilityLab.Api.Endpoints.Products;
using ObservabilityLab.Api.Endpoints.WhatsApp;
using ObservabilityLab.Api.Middleware;
using ObservabilityLab.Alerting;
using ObservabilityLab.Application;
using ObservabilityLab.Infrastructure;
using ObservabilityLab.Infrastructure.Data;
using ObservabilityLab.Infrastructure.Health;
using ObservabilityLab.Observability;
using ObservabilityLab.Observability.Dashboard;
using ObservabilityLab.Observability.Metrics;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Enrichers;
// ─── Serilog Bootstrap ────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/observability-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// ─── Cada camada registra suas próprias dependências ─────────────────────────
builder.Services
    .AddWebServices()
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddObservability(builder.Configuration)
    .AddAlerting(builder.Configuration);

// ─── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ─── Pipeline HTTP ────────────────────────────────────────────────────────────
app.UseSwagger();

app.UseSwaggerUI(opts =>
{
    opts.SwaggerEndpoint("/swagger/v1/swagger.json", "ObservabilityLab v1");
    opts.RoutePrefix = "docs";
});

// Redireciona a raiz para o Swagger UI
app.MapGet("/", () => Results.Redirect("/docs")).ExcludeFromDescription();

app.UseCors();

app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("UserAgent", ctx.Request.Headers.UserAgent.ToString());
        diag.Set("RemoteIP", ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    };
});

// ─── Middleware ───────────────────────────────────────────────────────────────
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RequestTimingMiddleware>();
app.UseMiddleware<RequestCounterMiddleware>();

// ─── Prometheus ───────────────────────────────────────────────────────────────
app.MapPrometheusScrapingEndpoint("/metrics");

// ─── Endpoints ────────────────────────────────────────────────────────────────
app.MapProductsEndpoints();
app.MapAuthEndpoints();
app.MapDiagnosticEndpoints();
app.MapDashboardEndpoints();
app.MapWhatsAppEndpoints();

// ─── Health Checks ────────────────────────────────────────────────────────────
app.MapHealthChecks("/health", new()
{
    ResponseWriter = HealthCheckResponseWriter.WriteDetailedJson
});

app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("critical")
});

// ─── Startup ──────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();

    await DbSeeder.SeedAsync(db);
}

app.Run();