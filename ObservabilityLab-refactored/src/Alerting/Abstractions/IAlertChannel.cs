using ObservabilityLab.Alerting.Domain;

namespace ObservabilityLab.Alerting.Abstractions;

/// <summary>
/// Contrato de canal de alerta — Strategy Pattern.
/// Cada implementação (WhatsApp, Email, Telegram) é intercambiável.
/// Chain of Responsibility: canais tentados em ordem de Priority.
/// true = entregue (chain para). false/exception = próximo canal tentado.
/// </summary>
public interface IAlertChannel
{
    string Name      { get; }
    int    Priority  { get; }  // Menor = tentado primeiro
    bool   IsEnabled { get; }

    Task<bool> SendAsync(Alert alert, CancellationToken ct = default);
}
