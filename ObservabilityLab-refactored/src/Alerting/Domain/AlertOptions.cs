namespace ObservabilityLab.Alerting.Domain;

/// <summary>
/// Configurações de alertas mapeadas do appsettings.json (seção "Alerting").
/// </summary>
public sealed class AlertOptions
{
    public bool   EmailEnabled     { get; set; }
    public string EmailRecipient   { get; set; } = string.Empty;
    public bool   TelegramEnabled  { get; set; }
    public string TelegramBotToken { get; set; } = string.Empty;
    public string TelegramChatId   { get; set; } = string.Empty;
    public bool   WhatsAppEnabled  { get; set; }
    public string WhatsAppApiUrl   { get; set; } = string.Empty;
    public string WhatsAppApiKey   { get; set; } = string.Empty;
    public string WhatsAppTo       { get; set; } = string.Empty;
}
