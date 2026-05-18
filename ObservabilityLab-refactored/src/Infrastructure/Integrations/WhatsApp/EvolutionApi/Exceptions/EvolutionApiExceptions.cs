namespace ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Exceptions;

/// <summary>Exceção base para falhas da Evolution API.</summary>
public class EvolutionApiException(string message, int? statusCode = null, Exception? inner = null)
    : Exception(message, inner)
{
    public int? StatusCode { get; } = statusCode;
}

/// <summary>Instância não encontrada ou não conectada.</summary>
public sealed class EvolutionApiInstanceNotFoundException(string instanceName)
    : EvolutionApiException($"Evolution API instance '{instanceName}' not found or not connected.", 404);

/// <summary>Falha de autenticação (API key inválida).</summary>
public sealed class EvolutionApiAuthenticationException()
    : EvolutionApiException("Evolution API authentication failed. Check your API key.", 401);

/// <summary>Rate limiting da Evolution API.</summary>
public sealed class EvolutionApiRateLimitException()
    : EvolutionApiException("Evolution API rate limit reached. Retry after backoff.", 429);

/// <summary>Circuit breaker aberto — Evolution API considerada indisponível.</summary>
public sealed class EvolutionApiCircuitOpenException()
    : EvolutionApiException("Evolution API circuit breaker is OPEN. Service temporarily unavailable.");

/// <summary>Timeout ao aguardar resposta da Evolution API.</summary>
public sealed class EvolutionApiTimeoutException(string operation)
    : EvolutionApiException($"Evolution API timeout during operation: {operation}");
