using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObservabilityLab.Application;
using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Application.Features.WhatsApp.CreateInstance;
using ObservabilityLab.Application.Features.WhatsApp.SendMediaMessage;
using ObservabilityLab.Application.Features.WhatsApp.SendTextMessage;
using ObservabilityLab.Infrastructure.Data;
using ObservabilityLab.Infrastructure.Diagnostics;
using ObservabilityLab.Infrastructure.Health;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Clients;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Handlers;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Options;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Services;
using ObservabilityLab.Infrastructure.Services;
using Microsoft.Extensions.Http.Resilience;
namespace ObservabilityLab.Infrastructure;

/// <summary>
/// Registro completo da camada Infrastructure.
/// Grupos de serviços em métodos privados para clareza e Single Responsibility.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddDatabase(configuration)
            .AddCaching(configuration)
            .AddInfrastructureHealthChecks(configuration)
            .AddEvolutionApi(configuration)
            .AddInfrastructureServices();

        return services;
    }

    private static IServiceCollection AddDatabase(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SlowQueryInterceptor>();
        services.AddDbContext<AppDbContext>((sp, opts) =>
            opts.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .EnableSensitiveDataLogging(IsDevEnvironment())
                .EnableDetailedErrors(IsDevEnvironment())
                .AddInterceptors(sp.GetRequiredService<SlowQueryInterceptor>()));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        return services;
    }

    private static IServiceCollection AddCaching(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddStackExchangeRedisCache(opts =>
        {
            opts.Configuration = configuration.GetConnectionString("Redis");
            opts.InstanceName  = "ObservabilityLab:";
        });
        return services;
    }

    private static IServiceCollection AddInfrastructureHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("DefaultConnection")!,
                name: "postgres", tags: ["db", "sql", "critical"])
            .AddRedis(configuration.GetConnectionString("Redis")!,
                name: "redis", tags: ["cache", "redis"])
            .AddCheck<SystemResourcesHealthCheck>("system-resources", tags: ["system", "resources"]);
        return services;
    }

    private static IServiceCollection AddEvolutionApi(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EvolutionApiOptions>()
            .Bind(configuration.GetSection(EvolutionApiOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var opts = configuration
            .GetSection(EvolutionApiOptions.Section)
            .Get<EvolutionApiOptions>() ?? new EvolutionApiOptions();

        // Typed HttpClient com Polly completo (Retry + Circuit Breaker + Timeout)
        services.AddHttpClient<EvolutionApiClient>(client =>
            {
                if (!string.IsNullOrEmpty(opts.BaseUrl))
                    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
                if (!string.IsNullOrEmpty(opts.ApiKey))
                    client.DefaultRequestHeaders.Add("apikey", opts.ApiKey);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler();

        // Serviços internos da Evolution API
        services.AddScoped<EvolutionApiMessageService>();
        services.AddScoped<EvolutionApiInstanceService>();
        services.AddScoped<EvolutionApiWebhookHandler>();

        // Adapters: implementam interfaces da Application (Dependency Inversion)
        services.AddScoped<WhatsAppMessageServiceAdapter>();
        services.AddScoped<IWhatsAppMessageService>(sp => sp.GetRequiredService<WhatsAppMessageServiceAdapter>());
        services.AddScoped<IWhatsAppMediaService>(sp => sp.GetRequiredService<WhatsAppMessageServiceAdapter>());
        services.AddScoped<IWhatsAppInstanceService, WhatsAppInstanceServiceAdapter>();

        // HTTP clients para alertas com resiliência
        services.AddHttpClient("telegram")
    .AddStandardResilienceHandler();

        services.AddHttpClient("whatsapp")
            .AddStandardResilienceHandler();

        return services;
    }

    private static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddHostedService<DbConnectionPoolMonitorService>();
        return services;
    }

    private static bool IsDevEnvironment()
        => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
}
