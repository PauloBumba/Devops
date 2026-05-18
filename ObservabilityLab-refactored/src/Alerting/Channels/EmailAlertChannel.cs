using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObservabilityLab.Alerting.Abstractions;
using ObservabilityLab.Alerting.Domain;

namespace ObservabilityLab.Alerting.Channels;

/// <summary>Canal Email — prioridade 2. Stub: substituir por SendGrid/SES em produção.</summary>
public sealed class EmailAlertChannel(
    IOptions<AlertOptions>    options,
    ILogger<EmailAlertChannel> logger) : IAlertChannel
{
    private readonly AlertOptions _opts = options.Value;

    public string Name      => "Email";
    public int    Priority  => 2;
    public bool   IsEnabled => _opts.EmailEnabled;

    public async Task<bool> SendAsync(Alert alert, CancellationToken ct = default)
    {
        if (!IsEnabled) return false;

        logger.LogDebug("Sending alert via Email: {AlertTitle}", alert.Title);

        // TODO: substituir por SendGrid, Amazon SES ou SMTP
        await Task.Delay(100, ct);

        logger.LogInformation(
            "📧 [EMAIL STUB] To: {Recipient} | [{Severity}] {Title} — {Message}",
            _opts.EmailRecipient, alert.Severity, alert.Title, alert.Message);

        return true;
    }
}
