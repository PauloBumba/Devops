using System.Text.Json.Serialization;

namespace ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Models;

// ─── Instance ────────────────────────────────────────────────────────────────

public sealed record CreateInstanceRequest(
    [property: JsonPropertyName("instanceName")] string InstanceName,
    [property: JsonPropertyName("qrcode")]       bool   QrCode = true,
    [property: JsonPropertyName("integration")]  string Integration = "WHATSAPP-BAILEYS");

public sealed record InstanceResponse(
    [property: JsonPropertyName("instance")]  InstanceInfo  Instance,
    [property: JsonPropertyName("hash")]      HashInfo?     Hash,
    [property: JsonPropertyName("qrcode")]    QrCodeInfo?   QrCode);

public sealed record InstanceInfo(
    [property: JsonPropertyName("instanceName")] string InstanceName,
    [property: JsonPropertyName("status")]       string Status);

public sealed record HashInfo(
    [property: JsonPropertyName("apikey")] string ApiKey);

public sealed record QrCodeInfo(
    [property: JsonPropertyName("pairingCode")] string? PairingCode,
    [property: JsonPropertyName("code")]        string? Code,
    [property: JsonPropertyName("base64")]      string? Base64);

public sealed record InstanceStatusResponse(
    [property: JsonPropertyName("instance")] InstanceStatusInfo Instance);

public sealed record InstanceStatusInfo(
    [property: JsonPropertyName("instanceName")] string InstanceName,
    [property: JsonPropertyName("state")]        string State);

// ─── Messages ────────────────────────────────────────────────────────────────

public sealed record SendTextMessageRequest(
    [property: JsonPropertyName("number")]  string      Number,
    [property: JsonPropertyName("options")] SendOptions Options,
    [property: JsonPropertyName("textMessage")] TextBody TextMessage);

public sealed record TextBody(
    [property: JsonPropertyName("text")] string Text);

public sealed record SendOptions(
    [property: JsonPropertyName("delay")]       int    Delay = 1200,
    [property: JsonPropertyName("presence")]    string Presence = "composing",
    [property: JsonPropertyName("linkPreview")] bool   LinkPreview = false);

public sealed record SendMediaMessageRequest(
    [property: JsonPropertyName("number")]       string     Number,
    [property: JsonPropertyName("options")]      SendOptions Options,
    [property: JsonPropertyName("mediaMessage")] MediaBody   MediaMessage);

public sealed record MediaBody(
    [property: JsonPropertyName("mediatype")] string MediaType,
    [property: JsonPropertyName("caption")]   string Caption,
    [property: JsonPropertyName("media")]     string Media,
    [property: JsonPropertyName("fileName")]  string FileName = "");

public sealed record MessageResponse(
    [property: JsonPropertyName("key")]    MessageKey  Key,
    [property: JsonPropertyName("status")] string      Status);

public sealed record MessageKey(
    [property: JsonPropertyName("remoteJid")] string RemoteJid,
    [property: JsonPropertyName("fromMe")]    bool   FromMe,
    [property: JsonPropertyName("id")]        string Id);

// ─── Webhook ─────────────────────────────────────────────────────────────────

public sealed record WebhookPayload(
    [property: JsonPropertyName("event")]    string      Event,
    [property: JsonPropertyName("instance")] string      Instance,
    [property: JsonPropertyName("data")]     object?     Data,
    [property: JsonPropertyName("date_time")] string     DateTime);

// ─── Error ───────────────────────────────────────────────────────────────────

public sealed record EvolutionApiError(
    [property: JsonPropertyName("status")]  int    Status,
    [property: JsonPropertyName("error")]   string Error,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("response")] EvolutionApiErrorDetail? Response);

public sealed record EvolutionApiErrorDetail(
    [property: JsonPropertyName("message")] object Message);
