using Microsoft.Extensions.DependencyInjection;
using ObservabilityLab.Alerting.Abstractions;
using ObservabilityLab.Alerting.BackgroundServices;
using ObservabilityLab.Alerting.Channels;
using ObservabilityLab.Alerting.Domain;
using ObservabilityLab.Alerting.Policies;
using ObservabilityLab.Alerting.Services;

namespace ObservabilityLab.Alerting;

/// <summary>
/// Registro da camada Alerting: canais, políticas, serviço e background monitor.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAlerting(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        // ── Configuração ──────────────────────────────────────────────────
        services.Configure<AlertOptions>(configuration.GetSection("Alerting"));

        // ── Canais (Chain of Responsibility) ──────────────────────────────
        // Registrados como IAlertChannel; AlertService resolve IEnumerable<IAlertChannel>
        services.AddSingleton<IAlertChannel, WhatsAppAlertChannel>();
        services.AddSingleton<IAlertChannel, EmailAlertChannel>();
        services.AddSingleton<IAlertChannel, TelegramAlertChannel>();

        // ── Políticas de alerta (Strategy) ────────────────────────────────
        services.AddSingleton<IAlertPolicy, ThresholdAlertPolicy>();

        // ── Serviço orquestrador ──────────────────────────────────────────
        services.AddSingleton<AlertService>();

        // ── HTTP clients para os canais ───────────────────────────────────
        services.AddHttpClient("telegram");
        services.AddHttpClient("whatsapp");

        // ── Background monitor ────────────────────────────────────────────
        services.AddHostedService<AlertMonitorService>();

        return services;
    }
}
