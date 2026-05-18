using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObservabilityLab.Alerting.Abstractions;
using ObservabilityLab.Alerting.Domain;
using ObservabilityLab.Domain.Enums;

namespace ObservabilityLab.Alerting.Channels;

/// <summary>Canal Telegram — prioridade 3 (último recurso).</summary>
public sealed class TelegramAlertChannel(
    IHttpClientFactory              factory,
    IOptions<AlertOptions>          options,
    ILogger<TelegramAlertChannel>   logger) : IAlertChannel
{
    private readonly AlertOptions _opts = options.Value;

    public string Name      => "Telegram";
    public int    Priority  => 3;
    public bool   IsEnabled => _opts.TelegramEnabled;

    public async Task<bool> SendAsync(Alert alert, CancellationToken ct = default)
    {
        if (!IsEnabled) return false;

        logger.LogDebug("Sending alert via Telegram: {AlertTitle}", alert.Title);

        var client   = factory.CreateClient("telegram");
        var url      = $"https://api.telegram.org/bot{_opts.TelegramBotToken}/sendMessage";
        var payload  = new
        {
            chat_id    = _opts.TelegramChatId,
            parse_mode = "Markdown",
            text       = FormatMessage(alert)
        };

        var response = await client.PostAsJsonAsync(url, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Telegram alert failed: {StatusCode}", response.StatusCode);
            return false;
        }

        logger.LogInformation("✅ Telegram alert sent: {AlertTitle}", alert.Title);
        return true;
    }

    private static string FormatMessage(Alert alert)
    {
        var emoji = alert.Severity switch
        {
            AlertSeverity.Critical => "🔴",
            AlertSeverity.Warning  => "🟡",
            _                     => "🟢"
        };
        return $"{emoji} *{alert.Severity.ToString().ToUpper()}* — {alert.Title}\n\n" +
               $"{alert.Message}\n\n" +
               $"📍 Source: `{alert.Source}`\n" +
               $"🕐 {alert.OccurredAt:yyyy-MM-dd HH:mm:ss} UTC";
    }
}
