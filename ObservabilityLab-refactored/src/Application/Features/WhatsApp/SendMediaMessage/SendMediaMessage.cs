using MediatR;
using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Application.Common;

namespace ObservabilityLab.Application.Features.WhatsApp.SendMediaMessage;

// ─── Contracts ────────────────────────────────────────────────────────────────

public interface IWhatsAppMediaService
{
    Task<SendMediaMessageResponse> SendMediaAsync(
        string to, string mediaUrl, string mediaType, string caption,
        string? instanceName = null, CancellationToken ct = default);
}

// ─── Command ──────────────────────────────────────────────────────────────────

public sealed record SendMediaMessageCommand(
    string  To,
    string  MediaUrl,
    string  MediaType,
    string  Caption,
    string? InstanceName = null) : IRequest<SendMediaMessageResponse>;

// ─── Response ─────────────────────────────────────────────────────────────────

public sealed record SendMediaMessageResponse(
    string         MessageId,
    string         To,
    string         MediaType,
    string         Status,
    DateTimeOffset SentAt);

// ─── Validator ────────────────────────────────────────────────────────────────

public sealed class SendMediaMessageValidator : IValidator<SendMediaMessageCommand>
{
    private static readonly HashSet<string> AllowedMediaTypes =
        ["image", "video", "audio", "document"];

    public ValidationResult Validate(SendMediaMessageCommand cmd)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(cmd.To))
            result.AddError("Recipient number (To) is required.");

        if (string.IsNullOrWhiteSpace(cmd.MediaUrl))
            result.AddError("Media URL is required.");

        if (!Uri.TryCreate(cmd.MediaUrl, UriKind.Absolute, out _))
            result.AddError("Media URL must be a valid absolute URI.");

        if (!AllowedMediaTypes.Contains(cmd.MediaType?.ToLower() ?? ""))
            result.AddError($"MediaType must be one of: {string.Join(", ", AllowedMediaTypes)}.");

        if (cmd.Caption?.Length > 1024)
            result.AddError("Caption cannot exceed 1024 characters.");

        return result;
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────

public sealed class SendMediaMessageHandler(IWhatsAppMediaService whatsApp)
    : IRequestHandler<SendMediaMessageCommand, SendMediaMessageResponse>
{
    public async Task<SendMediaMessageResponse> Handle(
        SendMediaMessageCommand command,
        CancellationToken       ct)
        => await whatsApp.SendMediaAsync(
            command.To, command.MediaUrl, command.MediaType,
            command.Caption, command.InstanceName, ct);
}
