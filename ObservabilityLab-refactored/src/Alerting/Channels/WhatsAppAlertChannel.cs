using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObservabilityLab.Alerting.Abstractions;
using ObservabilityLab.Alerting.Domain;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ObservabilityLab.Alerting.Channels
{

    public sealed class WhatsAppAlertChannel(
        IHttpClientFactory factory,
        IOptions<AlertOptions> options,
        ILogger<WhatsAppAlertChannel> logger) : IAlertChannel
    {
        private readonly AlertOptions _opts = options.Value;

        public string Name => "WhatsApp";
        public int Priority => 1;
        public bool IsEnabled => _opts.WhatsAppEnabled;

        public async Task<bool> SendAsync(Alert alert, CancellationToken ct = default)
        {
            if (!IsEnabled) return false;

            logger.LogDebug("Sending alert via WhatsApp: {AlertTitle}", alert.Title);

            var client = factory.CreateClient("whatsapp");
            var payload = new
            {
                messaging_product = "whatsapp",
                to = _opts.WhatsAppTo,
                type = "text",
                text = new { body = FormatMessage(alert) }
            };

            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_opts.WhatsAppApiKey}");
            var response = await client.PostAsJsonAsync(_opts.WhatsAppApiUrl, payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("WhatsApp alert failed: {StatusCode}", response.StatusCode);
                return false;
            }

            logger.LogInformation("✅ WhatsApp alert sent: {AlertTitle}", alert.Title);
            return true;
        }

        private static string FormatMessage(Alert alert) =>
            $"🚨 *{alert.Severity}* | {alert.Title}\n" +
            $"{alert.Message}\n" +
            $"_Source: {alert.Source} at {alert.OccurredAt:HH:mm:ss UTC}_";
    }
}