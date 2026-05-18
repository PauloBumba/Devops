using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Exceptions;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Models;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Options;
using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Clients;

/// <summary>
/// Typed HTTP Client centralizado para a Evolution API.
///
/// Responsabilidades:
///   - Autenticação por header (apikey)
///   - Serialização/deserialização padronizada
///   - Distributed tracing por request
///   - Structured logging com CorrelationId
///   - Tratamento unificado de erros HTTP → exceções de domínio
///   - NÃO contém lógica de retry (delegado ao Polly no registro)
/// </summary>
public sealed class EvolutionApiClient(
    HttpClient                     httpClient,
    IOptions<EvolutionApiOptions>  options,
    AppDiagnostics                 diagnostics,
    ILogger<EvolutionApiClient>    logger)
{
    private readonly EvolutionApiOptions _opts = options.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // ── Instance Operations ───────────────────────────────────────────────────

    public async Task<InstanceResponse> CreateInstanceAsync(
        CreateInstanceRequest request,
        CancellationToken     ct = default)
        => await PostAsync<CreateInstanceRequest, InstanceResponse>("instance/create", request, ct);

    public async Task<InstanceStatusResponse> GetInstanceStatusAsync(
        string            instanceName,
        CancellationToken ct = default)
        => await GetAsync<InstanceStatusResponse>($"instance/connectionState/{instanceName}", ct);

    public async Task DeleteInstanceAsync(string instanceName, CancellationToken ct = default)
        => await DeleteAsync($"instance/delete/{instanceName}", ct);

    public async Task<QrCodeInfo> GetQrCodeAsync(string instanceName, CancellationToken ct = default)
        => await GetAsync<QrCodeInfo>($"instance/qrcode/{instanceName}", ct);

    public async Task DisconnectInstanceAsync(string instanceName, CancellationToken ct = default)
        => await DeleteAsync($"instance/logout/{instanceName}", ct);

    // ── Message Operations ────────────────────────────────────────────────────

    public async Task<MessageResponse> SendTextAsync(
        string                 instanceName,
        SendTextMessageRequest request,
        CancellationToken      ct = default)
        => await PostAsync<SendTextMessageRequest, MessageResponse>(
            $"message/sendText/{instanceName}", request, ct);

    public async Task<MessageResponse> SendMediaAsync(
        string                  instanceName,
        SendMediaMessageRequest request,
        CancellationToken       ct = default)
        => await PostAsync<SendMediaMessageRequest, MessageResponse>(
            $"message/sendMedia/{instanceName}", request, ct);

    // ── HTTP Primitives ───────────────────────────────────────────────────────

    private async Task<TResponse> GetAsync<TResponse>(string path, CancellationToken ct)
    {
        using var activity = diagnostics.StartBusinessActivity($"evolution.GET.{path}");
        activity?.SetTag("evolution.path", path);

        logger.LogDebug("Evolution API GET {Path}", path);

        var response = await httpClient.GetAsync(path, ct);
        return await DeserializeResponseAsync<TResponse>(response, "GET", path, ct);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string  path, TRequest body, CancellationToken ct)
    {
        using var activity = diagnostics.StartBusinessActivity($"evolution.POST.{path}");
        activity?.SetTag("evolution.path", path);

        logger.LogDebug("Evolution API POST {Path}", path);

        var response = await httpClient.PostAsJsonAsync(path, body, JsonOptions, ct);
        return await DeserializeResponseAsync<TResponse>(response, "POST", path, ct);
    }

    private async Task DeleteAsync(string path, CancellationToken ct)
    {
        using var activity = diagnostics.StartBusinessActivity($"evolution.DELETE.{path}");
        activity?.SetTag("evolution.path", path);

        logger.LogDebug("Evolution API DELETE {Path}", path);

        var response = await httpClient.DeleteAsync(path, ct);
        await EnsureSuccessAsync(response, "DELETE", path, ct);
    }

    // ── Error Handling ────────────────────────────────────────────────────────

    private async Task<TResponse> DeserializeResponseAsync<TResponse>(
        HttpResponseMessage response,
        string              method,
        string              path,
        CancellationToken   ct)
    {
        await EnsureSuccessAsync(response, method, path, ct);

        var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);

        if (result is null)
            throw new EvolutionApiException(
                $"Evolution API returned null response for {method} {path}");

        logger.LogDebug("Evolution API {Method} {Path} → {StatusCode}", method, path, response.StatusCode);
        return result;
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string              method,
        string              path,
        CancellationToken   ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body    = await response.Content.ReadAsStringAsync(ct);
        var status  = (int)response.StatusCode;

        logger.LogError(
            "Evolution API error — {Method} {Path} | Status: {Status} | Body: {Body}",
            method, path, status, body);

        throw status switch
        {
            401 => new EvolutionApiAuthenticationException(),
            404 => new EvolutionApiInstanceNotFoundException(path),
            429 => new EvolutionApiRateLimitException(),
            _   => new EvolutionApiException($"Evolution API {method} {path} failed ({status}): {body}", status)
        };
    }
}
