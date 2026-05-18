using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Alerting.Abstractions;
using ObservabilityLab.Alerting.Domain;
using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Alerting.Services;

// ─── Result types ─────────────────────────────────────────────────────────────

public sealed record ChannelAttempt(string Channel, bool Succeeded, string? Error, long ElapsedMs);
public sealed record AlertDeliveryResult(bool Delivered, string? DeliveredVia, IReadOnlyList<ChannelAttempt> Attempts);

// ─── Service ──────────────────────────────────────────────────────────────────

/// <summary>
/// Orchestra os canais de alerta via Chain of Responsibility:
/// tenta cada canal em ordem de Priority até um ter sucesso.
/// </summary>
public sealed class AlertService(
    IEnumerable<IAlertChannel> channels,
    AppDiagnostics             diagnostics,
    ILogger<AlertService>      logger)
{
    private readonly IReadOnlyList<IAlertChannel> _channels =
        channels.OrderBy(c => c.Priority).ToList();

    public async Task<AlertDeliveryResult> SendWithFallbackAsync(Alert alert, CancellationToken ct = default)
    {
        using var activity = diagnostics.StartAlertActivity("fallback-chain");
        activity?.SetTag("alert.id",       alert.Id.ToString());
        activity?.SetTag("alert.severity", alert.Severity.ToString());

        var attempts = new List<ChannelAttempt>();

        foreach (var channel in _channels)
        {
            if (!channel.IsEnabled)
            {
                logger.LogDebug("Skipping disabled channel: {Channel}", channel.Name);
                continue;
            }

            using var channelActivity = diagnostics.StartAlertActivity(channel.Name);
            var sw = Stopwatch.StartNew();

            try
            {
                var sent = await channel.SendAsync(alert, ct);
                sw.Stop();

                attempts.Add(new ChannelAttempt(channel.Name, sent, null, sw.ElapsedMilliseconds));

                if (sent)
                {
                    channelActivity?.SetTag("alert.delivered", true);
                    activity?.SetTag("alert.delivered_via", channel.Name);
                    logger.LogInformation(
                        "Alert delivered via {Channel} in {Elapsed}ms: {Title}",
                        channel.Name, sw.ElapsedMilliseconds, alert.Title);

                    return new AlertDeliveryResult(true, channel.Name, attempts);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                attempts.Add(new ChannelAttempt(channel.Name, false, ex.Message, sw.ElapsedMilliseconds));
                AppDiagnostics.RecordException(channelActivity, ex);
                logger.LogWarning(ex, "Alert channel {Channel} failed, trying next", channel.Name);
            }
        }

        activity?.SetTag("alert.delivered", false);
        logger.LogError("All alert channels exhausted for alert {AlertId}: {Title}", alert.Id, alert.Title);

        return new AlertDeliveryResult(false, null, attempts);
    }
}
