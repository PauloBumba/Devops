using MediatR;
using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Application.Common;

namespace ObservabilityLab.Application.Features.WhatsApp.SendTextMessage;

// ─── Contracts ────────────────────────────────────────────────────────────────

public interface IWhatsAppMessageService
{
    Task<SendTextMessageResponse> SendTextAsync(
        string to, string text, string? instanceName = null, CancellationToken ct = default);
}

// ─── Command ──────────────────────────────────────────────────────────────────

public sealed record SendTextMessageCommand(
    string  To,
    string  Text,
    string? InstanceName = null) : IRequest<SendTextMessageResponse>;

// ─── Response ─────────────────────────────────────────────────────────────────

public sealed record SendTextMessageResponse(
    string MessageId,
    string To,
    string Status,
    DateTimeOffset SentAt);

// ─── Validator ────────────────────────────────────────────────────────────────

public sealed class SendTextMessageValidator : IValidator<SendTextMessageCommand>
{
    public ValidationResult Validate(SendTextMessageCommand cmd)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(cmd.To))
            result.AddError("Recipient number (To) is required.");

        if (!cmd.To?.StartsWith('+') == true && cmd.To?.All(char.IsDigit) == false)
            result.AddError("Recipient must be a valid phone number.");

        if (string.IsNullOrWhiteSpace(cmd.Text))
            result.AddError("Message text is required.");

        if (cmd.Text?.Length > 4096)
            result.AddError("Message text cannot exceed 4096 characters.");

        return result;
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────

public sealed class SendTextMessageHandler(IWhatsAppMessageService whatsApp)
    : IRequestHandler<SendTextMessageCommand, SendTextMessageResponse>
{
    public async Task<SendTextMessageResponse> Handle(
        SendTextMessageCommand command,
        CancellationToken      ct)
        => await whatsApp.SendTextAsync(command.To, command.Text, command.InstanceName, ct);
}
