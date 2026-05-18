using ObservabilityLab.Application.Features.WhatsApp.CreateInstance;
using ObservabilityLab.Application.Features.WhatsApp.GetQrCode;
using ObservabilityLab.Application.Features.WhatsApp.SendMediaMessage;
using ObservabilityLab.Application.Features.WhatsApp.SendTextMessage;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Services;

namespace ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Services;

/// <summary>
/// Adapta EvolutionApiMessageService para a interface IWhatsAppMessageService da Application.
/// Dependency Inversion: Application define o contrato, Infrastructure implementa.
/// </summary>
public sealed class WhatsAppMessageServiceAdapter(EvolutionApiMessageService messageService)
    : IWhatsAppMessageService, IWhatsAppMediaService
{
    // ── IWhatsAppMessageService ───────────────────────────────────────────────

    public async Task<SendTextMessageResponse> SendTextAsync(
        string            to,
        string            text,
        string?           instanceName = null,
        CancellationToken ct           = default)
    {
        var response = await messageService.SendTextAsync(to, text, instanceName, ct);

        return new SendTextMessageResponse(
            MessageId: response.Key.Id,
            To:        response.Key.RemoteJid,
            Status:    response.Status,
            SentAt:    DateTimeOffset.UtcNow);
    }

    // ── IWhatsAppMediaService ─────────────────────────────────────────────────

    public async Task<SendMediaMessageResponse> SendMediaAsync(
        string            to,
        string            mediaUrl,
        string            mediaType,
        string            caption,
        string?           instanceName = null,
        CancellationToken ct           = default)
    {
        var response = await messageService.SendMediaAsync(to, mediaUrl, mediaType, caption, instanceName, ct);

        return new SendMediaMessageResponse(
            MessageId: response.Key.Id,
            To:        response.Key.RemoteJid,
            MediaType: mediaType,
            Status:    response.Status,
            SentAt:    DateTimeOffset.UtcNow);
    }
}

/// <summary>
/// Adapta EvolutionApiInstanceService para a interface IWhatsAppInstanceService da Application.
/// </summary>
public sealed class WhatsAppInstanceServiceAdapter(EvolutionApiInstanceService instanceService)
    : IWhatsAppInstanceService
{
    public async Task<CreateInstanceResponse> CreateAsync(
        string instanceName, bool generateQrCode, CancellationToken ct)
    {
        var response = await instanceService.CreateInstanceAsync(instanceName, generateQrCode, ct);

        return new CreateInstanceResponse(
            InstanceName: response.Instance.InstanceName,
            Status:       response.Instance.Status,
            QrCodeBase64: response.QrCode?.Base64);
    }

    public Task<string> GetStateAsync(string instanceName, CancellationToken ct)
        => instanceService.GetInstanceStateAsync(instanceName, ct);

    public async Task<GetQrCodeResponse> GetQrCodeAsync(string instanceName, CancellationToken ct)
    {
        var qrCode = await instanceService.GetQrCodeAsync(instanceName, ct);

        return new GetQrCodeResponse(
            InstanceName: instanceName,
            Base64:       qrCode.Base64,
            PairingCode:  qrCode.PairingCode);
    }

    public Task DisconnectAsync(string instanceName, CancellationToken ct)
        => instanceService.DisconnectAsync(instanceName, ct);

    public Task DeleteAsync(string instanceName, CancellationToken ct)
        => instanceService.DeleteAsync(instanceName, ct);
}
