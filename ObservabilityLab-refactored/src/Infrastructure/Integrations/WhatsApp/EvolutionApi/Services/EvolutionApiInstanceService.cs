using Microsoft.Extensions.Logging;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Clients;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Models;
using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Services;

/// <summary>
/// Gerencia o ciclo de vida de instâncias WhatsApp na Evolution API.
/// Responsabilidade única: CRUD de instâncias e QR Code.
/// </summary>
public sealed class EvolutionApiInstanceService(
    EvolutionApiClient                  client,
    AppDiagnostics                      diagnostics,
    ILogger<EvolutionApiInstanceService> logger)
{
    public async Task<InstanceResponse> CreateInstanceAsync(
        string            instanceName,
        bool              generateQrCode = true,
        CancellationToken ct             = default)
    {
        using var activity = diagnostics.StartBusinessActivity("whatsapp.createInstance");
        activity?.SetTag("whatsapp.instance", instanceName);

        logger.LogInformation("Creating WhatsApp instance: {InstanceName}", instanceName);

        var request  = new CreateInstanceRequest(instanceName, generateQrCode);
        var response = await client.CreateInstanceAsync(request, ct);

        logger.LogInformation(
            "WhatsApp instance created — {InstanceName} | Status: {Status}",
            instanceName, response.Instance.Status);

        return response;
    }

    public async Task<string> GetInstanceStateAsync(
        string instanceName, CancellationToken ct = default)
    {
        using var activity = diagnostics.StartBusinessActivity("whatsapp.getInstanceState");
        activity?.SetTag("whatsapp.instance", instanceName);

        var status = await client.GetInstanceStatusAsync(instanceName, ct);

        logger.LogDebug(
            "WhatsApp instance {InstanceName} state: {State}",
            instanceName, status.Instance.State);

        return status.Instance.State;
    }

    public async Task<QrCodeInfo> GetQrCodeAsync(
        string instanceName, CancellationToken ct = default)
    {
        using var activity = diagnostics.StartBusinessActivity("whatsapp.getQrCode");
        activity?.SetTag("whatsapp.instance", instanceName);

        logger.LogInformation("Fetching QR code for instance: {InstanceName}", instanceName);

        var qrCode = await client.GetQrCodeAsync(instanceName, ct);

        logger.LogDebug("QR code fetched for instance: {InstanceName}", instanceName);
        return qrCode;
    }

    public async Task DisconnectAsync(string instanceName, CancellationToken ct = default)
    {
        using var activity = diagnostics.StartBusinessActivity("whatsapp.disconnect");
        activity?.SetTag("whatsapp.instance", instanceName);

        logger.LogInformation("Disconnecting WhatsApp instance: {InstanceName}", instanceName);
        await client.DisconnectInstanceAsync(instanceName, ct);
        logger.LogInformation("WhatsApp instance disconnected: {InstanceName}", instanceName);
    }

    public async Task DeleteAsync(string instanceName, CancellationToken ct = default)
    {
        using var activity = diagnostics.StartBusinessActivity("whatsapp.deleteInstance");
        activity?.SetTag("whatsapp.instance", instanceName);

        logger.LogWarning("Deleting WhatsApp instance: {InstanceName}", instanceName);
        await client.DeleteInstanceAsync(instanceName, ct);
        logger.LogInformation("WhatsApp instance deleted: {InstanceName}", instanceName);
    }
}
