using MediatR;
using ObservabilityLab.Application.Features.WhatsApp.CreateInstance;

using ObservabilityLab.Application.Features.WhatsApp.SendMediaMessage;
using ObservabilityLab.Application.Features.WhatsApp.SendTextMessage;

namespace ObservabilityLab.Api.Endpoints.WhatsApp;

/// <summary>
/// Endpoints da integração WhatsApp via Evolution API.
/// Responsabilidade: mapear HTTP → MediatR → retornar resultado padronizado.
/// Zero lógica de negócio aqui.
/// </summary>
public static class WhatsAppEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppEndpoints(this IEndpointRouteBuilder app)
    {
        var messages  = app.MapGroup("/api/v1/whatsapp/messages")
            .WithTags("WhatsApp — Messages")
            .WithOpenApi();

        var instances = app.MapGroup("/api/v1/whatsapp/instances")
            .WithTags("WhatsApp — Instances")
            .WithOpenApi();

        // ── Messages ──────────────────────────────────────────────────────
        messages.MapPost("/text",  SendText)
            .WithName("SendWhatsAppText")
            .WithSummary("Envia mensagem de texto via WhatsApp (Evolution API)");

        messages.MapPost("/media", SendMedia)
            .WithName("SendWhatsAppMedia")
            .WithSummary("Envia mídia (imagem, vídeo, documento) via WhatsApp");

        // ── Webhook ───────────────────────────────────────────────────────
        app.MapPost("/api/v1/whatsapp/webhook", HandleWebhook)
            .WithTags("WhatsApp — Webhook")
            .WithName("WhatsAppWebhook")
            .WithSummary("Endpoint de recebimento de webhooks da Evolution API")
            .WithOpenApi();

        // ── Instances ─────────────────────────────────────────────────────
        instances.MapPost("/",                  CreateInstance)
            .WithName("CreateWhatsAppInstance")
            .WithSummary("Cria uma nova instância WhatsApp");

        instances.MapGet("/{instanceName}/qrcode", GetQrCode)
            .WithName("GetWhatsAppQrCode")
            .WithSummary("Obtém o QR Code para conectar a instância");

        instances.MapDelete("/{instanceName}/disconnect", Disconnect)
            .WithName("DisconnectWhatsAppInstance")
            .WithSummary("Desconecta a instância WhatsApp (mantém a instância)");

        return app;
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> SendText(
        SendTextRequest   req,
        IMediator         mediator,
        CancellationToken ct)
    {
        var cmd    = new SendTextMessageCommand(req.To, req.Text, req.InstanceName);
        var result = await mediator.Send(cmd, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SendMedia(
        SendMediaRequest  req,
        IMediator         mediator,
        CancellationToken ct)
    {
        var cmd    = new SendMediaMessageCommand(req.To, req.MediaUrl, req.MediaType, req.Caption, req.InstanceName);
        var result = await mediator.Send(cmd, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleWebhook(
        Infrastructure.Integrations.WhatsApp.EvolutionApi.Models.WebhookPayload payload,
        Infrastructure.Integrations.WhatsApp.EvolutionApi.Handlers.EvolutionApiWebhookHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(payload, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateInstance(
        CreateInstanceRequest req,
        IMediator             mediator,
        CancellationToken     ct)
    {
        var cmd    = new CreateInstanceCommand(req.InstanceName, req.GenerateQrCode);
        var result = await mediator.Send(cmd, ct);
        return Results.Created($"/api/v1/whatsapp/instances/{result.InstanceName}/qrcode", result);
    }

    private static async Task<IResult> GetQrCode(
        string            instanceName,
        IMediator         mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetQrCodeQuery(instanceName), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Disconnect(
        string            instanceName,
        IMediator         mediator,
        CancellationToken ct)
    {
        await mediator.Send(new DisconnectInstanceCommand(instanceName), ct);
        return Results.NoContent();
    }
}

// ─── Request Bodies ───────────────────────────────────────────────────────────

public sealed record SendTextRequest(
    string  To,
    string  Text,
    string? InstanceName = null);

public sealed record SendMediaRequest(
    string  To,
    string  MediaUrl,
    string  MediaType,
    string  Caption,
    string? InstanceName = null);

public sealed record CreateInstanceRequest(
    string InstanceName,
    bool   GenerateQrCode = true);
