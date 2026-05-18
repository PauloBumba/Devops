using System.Text.Json;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Models;
using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Handlers;

/// <summary>
/// Processa webhooks recebidos da Evolution API.
///
/// Eventos suportados:
///   - messages.upsert  → nova mensagem recebida
///   - connection.update → mudança de estado da conexão
///   - qrcode.updated   → QR code regenerado
///   - messages.update  → atualização de status de mensagem
///
/// Responsabilidade única: roteamento de eventos → handlers específicos.
/// </summary>
public sealed class EvolutionApiWebhookHandler(
    AppMetrics                          metrics,
    AppDiagnostics                      diagnostics,
    ILogger<EvolutionApiWebhookHandler> logger)
{
    public async Task HandleAsync(WebhookPayload payload, CancellationToken ct = default)
    {
        using var activity = diagnostics.StartBusinessActivity($"webhook.{payload.Event}");
        activity?.SetTag("webhook.event",    payload.Event);
        activity?.SetTag("webhook.instance", payload.Instance);

        logger.LogInformation(
            "Webhook received — Event: {Event} | Instance: {Instance}",
            payload.Event, payload.Instance);

        metrics.RequestCount.Add(1, new TagList
        {
            { "webhook.event",    payload.Event },
            { "webhook.instance", payload.Instance }
        });

        try
        {
            await (payload.Event switch
            {
                "messages.upsert"   => HandleMessageReceivedAsync(payload, ct),
                "connection.update" => HandleConnectionUpdateAsync(payload, ct),
                "qrcode.updated"    => HandleQrCodeUpdatedAsync(payload, ct),
                "messages.update"   => HandleMessageStatusUpdateAsync(payload, ct),
                _                   => HandleUnknownEventAsync(payload, ct)
            });
        }
        catch (Exception ex)
        {
            AppDiagnostics.RecordException(activity, ex);
            logger.LogError(ex,
                "Error processing webhook event {Event} for instance {Instance}",
                payload.Event, payload.Instance);
            throw;
        }
    }

    private Task HandleMessageReceivedAsync(WebhookPayload payload, CancellationToken ct)
    {
        logger.LogInformation(
            "New WhatsApp message received on instance {Instance}",
            payload.Instance);

        // TODO: publicar evento de domínio ou chamar handler de use-case
        // Ex: await mediator.Publish(new WhatsAppMessageReceivedEvent(payload), ct);
        return Task.CompletedTask;
    }

    private Task HandleConnectionUpdateAsync(WebhookPayload payload, CancellationToken ct)
    {
        var data = payload.Data is JsonElement el
            ? el.GetProperty("state").GetString()
            : "unknown";

        logger.LogInformation(
            "WhatsApp connection state changed — Instance: {Instance} | State: {State}",
            payload.Instance, data);

        return Task.CompletedTask;
    }

    private Task HandleQrCodeUpdatedAsync(WebhookPayload payload, CancellationToken ct)
    {
        logger.LogInformation(
            "QR code updated for instance {Instance} — client should re-scan",
            payload.Instance);

        return Task.CompletedTask;
    }

    private Task HandleMessageStatusUpdateAsync(WebhookPayload payload, CancellationToken ct)
    {
        logger.LogDebug(
            "Message status updated on instance {Instance}",
            payload.Instance);

        return Task.CompletedTask;
    }

    private Task HandleUnknownEventAsync(WebhookPayload payload, CancellationToken ct)
    {
        logger.LogWarning(
            "Unknown Evolution API webhook event: {Event} | Instance: {Instance}",
            payload.Event, payload.Instance);

        return Task.CompletedTask;
    }
}
