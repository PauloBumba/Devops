using System.ComponentModel.DataAnnotations;

namespace ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Options;

/// <summary>
/// Configurações da Evolution API mapeadas do appsettings.json.
/// Seção: "EvolutionApi"
///
/// Exemplo appsettings.json:
/// "EvolutionApi": {
///   "BaseUrl":       "https://evolution.myserver.com",
///   "ApiKey":        "your-global-api-key",
///   "TimeoutSeconds": 30,
///   "RetryCount":    3,
///   "CircuitBreaker": { "FailureThreshold": 5, "DurationSeconds": 30 }
/// }
/// </summary>
public sealed class EvolutionApiOptions
{
    public const string Section = "EvolutionApi";

    [Required, Url]
    public string BaseUrl { get; init; } = string.Empty;

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 30;

    public int RetryCount { get; init; } = 3;

    public CircuitBreakerOptions CircuitBreaker { get; init; } = new();

    /// <summary>Nome da instância WhatsApp padrão (pode ser sobrescrito por request).</summary>
    public string DefaultInstance { get; init; } = "default";

    /// <summary>Versão da API (para versionamento de URL).</summary>
    public string ApiVersion { get; init; } = string.Empty;
}

public sealed class CircuitBreakerOptions
{
    /// <summary>Número de falhas para abrir o circuito.</summary>
    public int FailureThreshold { get; init; } = 5;

    /// <summary>Duração em segundos que o circuito permanece aberto.</summary>
    public int DurationSeconds { get; init; } = 30;

    /// <summary>Percentual mínimo de falhas para ativar o circuit breaker.</summary>
    public double FailureRatio { get; init; } = 0.5;
}
