using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Clients;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Models;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Options;
using ObservabilityLab.Observability.Metrics;
using System.Diagnostics;

namespace ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Services;

/// <summary>
/// Serviço de mensagens WhatsApp via Evolution API.
/// Responsabilidade única: envio de mensagens (texto e mídia).
/// Aplicação de observabilidade: métricas, tracing e logging estruturado.
/// </summary>
public sealed class EvolutionApiMessageService(
    EvolutionApiClient            client,
    IOptions<EvolutionApiOptions> options,
    AppMetrics                    metrics,
    AppDiagnostics                diagnostics,
    ILogger<EvolutionApiMessageService> logger)
{
    private readonly string _defaultInstance = options.Value.DefaultInstance;

    public async Task<MessageResponse> SendTextAsync(
        string            to,
        string            text,
        string?           instanceName = null,
        CancellationToken ct           = default)
    {
        var instance = instanceName ?? _defaultInstance;

        using var activity = diagnostics.StartBusinessActivity("whatsapp.sendText");
        activity?.SetTag("whatsapp.instance", instance);
        activity?.SetTag("whatsapp.to",       SanitizeNumber(to));

        var sw = Stopwatch.StartNew();
        logger.LogInformation(
            "Sending WhatsApp text to {To} via instance {Instance}",
            SanitizeNumber(to), instance);

        try
        {
            var request = new SendTextMessageRequest(
                Number:      to,
                Options:     new SendOptions(),
                TextMessage: new TextBody(text));

            var response = await client.SendTextAsync(instance, request, ct);
            sw.Stop();

            RecordMessageSent("text", instance, sw.Elapsed.TotalMilliseconds);
            logger.LogInformation(
                "WhatsApp text sent — MessageId: {MessageId} | To: {To} | {ElapsedMs}ms",
                response.Key.Id, SanitizeNumber(to), sw.Elapsed.TotalMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordMessageFailed("text", instance, ex.GetType().Name);
            AppDiagnostics.RecordException(activity, ex);
            logger.LogError(ex, "Failed to send WhatsApp text to {To}", SanitizeNumber(to));
            throw;
        }
    }

    public async Task<MessageResponse> SendMediaAsync(
        string            to,
        string            mediaUrl,
        string            mediaType,
        string            caption,
        string?           instanceName = null,
        CancellationToken ct           = default)
    {
        var instance = instanceName ?? _defaultInstance;

        using var activity = diagnostics.StartBusinessActivity("whatsapp.sendMedia");
        activity?.SetTag("whatsapp.instance",   instance);
        activity?.SetTag("whatsapp.to",         SanitizeNumber(to));
        activity?.SetTag("whatsapp.media_type", mediaType);

        var sw = Stopwatch.StartNew();
        logger.LogInformation(
            "Sending WhatsApp media ({MediaType}) to {To} via instance {Instance}",
            mediaType, SanitizeNumber(to), instance);

        try
        {
            var request = new SendMediaMessageRequest(
                Number:       to,
                Options:      new SendOptions(),
                MediaMessage: new MediaBody(mediaType, caption, mediaUrl));

            var response = await client.SendMediaAsync(instance, request, ct);
            sw.Stop();

            RecordMessageSent(mediaType, instance, sw.Elapsed.TotalMilliseconds);
            logger.LogInformation(
                "WhatsApp media sent — MessageId: {MessageId} | To: {To} | {ElapsedMs}ms",
                response.Key.Id, SanitizeNumber(to), sw.Elapsed.TotalMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordMessageFailed(mediaType, instance, ex.GetType().Name);
            AppDiagnostics.RecordException(activity, ex);
            logger.LogError(ex, "Failed to send WhatsApp media to {To}", SanitizeNumber(to));
            throw;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RecordMessageSent(string type, string instance, double elapsedMs)
    {
        metrics.DbQueryDuration.Record(elapsedMs, new TagList
        {
            { "whatsapp.operation", "send_message" },
            { "whatsapp.type",      type },
            { "whatsapp.instance",  instance },
            { "whatsapp.success",   "true" }
        });
    }

    private void RecordMessageFailed(string type, string instance, string exceptionType)
    {
        metrics.ErrorCount.Add(1, new TagList
        {
            { "whatsapp.operation",      "send_message" },
            { "whatsapp.type",           type },
            { "whatsapp.instance",       instance },
            { "whatsapp.exception_type", exceptionType }
        });
    }

    /// <summary>Remove últimos dígitos para não logar número completo (LGPD).</summary>
    private static string SanitizeNumber(string number)
        => number.Length > 6 ? $"{number[..6]}***" : "***";
}
