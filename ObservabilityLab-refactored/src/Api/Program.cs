using Serilog;
using Serilog.Enrichers.Span;
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

// ─── Serilog Bootstrap ────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/observability-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ─── Cada camada registra suas próprias dependências ─────────────────────────
builder.Services
    .AddWebServices()                                    // Web: Swagger, CORS, BackgroundServices
    .AddApplication()                                    // Application: MediatR, Validators, Behaviors
    .AddInfrastructure(builder.Configuration)            // Infrastructure: DB, Cache, EvolutionApi
    .AddObservability(builder.Configuration)             // Observability: OTel, Metrics, Tracing
    .AddAlerting(builder.Configuration);                 // Alerting: Channels, Policies, Monitor

// ─── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ─── Pipeline HTTP ────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opts =>
    {
        opts.SwaggerEndpoint("/swagger/v1/swagger.json", "ObservabilityLab v1");
        opts.RoutePrefix = "docs";
    });
}

app.UseCors();

app.UseSerilogRequestLogging(opts =>
1{
    opts.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("UserAgent", ctx.Request.Headers.UserAgent.ToString());
        diag.Set("RemoteIP",  ctx.Connection.RemoteIpAddress?.ToString());
    };
});

// ─── Middleware (ordem importa!) ──────────────────────────────────────────────
app.UseMiddleware<CorrelationIdMiddleware>();      // 1. Correlação primeiro
app.UseMiddleware<GlobalExceptionMiddleware>();    // 2. Captura exceções
app.UseMiddleware<RequestTimingMiddleware>();      // 3. Mede duração
app.UseMiddleware<RequestCounterMiddleware>();     // 4. Conta concorrência

// ─── Prometheus Scrape Endpoint ───────────────────────────────────────────────
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
app.MapHealthChecks("/health/live",  new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("critical")
});

// ─── Startup: Migrations + Seed ───────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.Run();
