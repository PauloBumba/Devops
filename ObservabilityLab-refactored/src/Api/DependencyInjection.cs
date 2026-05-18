using Microsoft.Extensions.DependencyInjection;
using ObservabilityLab.Api.BackgroundServices;

namespace ObservabilityLab.Api;

/// <summary>
/// Registro da camada Web/API:
/// Swagger, CORS, Background Services, Serilog, API Explorer.
/// Program.cs chama apenas services.AddWebServices() — sem acoplar detalhes.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWebServices(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(opts =>
        {
            opts.SwaggerDoc("v1", new()
            {
                Title       = "ObservabilityLab API",
                Version     = "v1",
                Description = "Enterprise Observability Platform with WhatsApp Integration (Evolution API)"
            });
        });

        services.AddCors(o =>
            o.AddDefaultPolicy(p =>
                p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

        // Background services da camada Web
        services.AddHostedService<SystemMetricsCollectorService>();
        services.AddHostedService<SlowRequestDetectorService>();

        return services;
    }
}
