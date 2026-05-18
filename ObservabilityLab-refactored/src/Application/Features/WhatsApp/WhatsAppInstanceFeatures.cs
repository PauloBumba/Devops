using MediatR;
using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Application.Common;


namespace ObservabilityLab.Application.Features.WhatsApp.CreateInstance
{

    public interface IWhatsAppInstanceService
    {
        Task<CreateInstanceResponse> CreateAsync(string instanceName, bool generateQrCode, CancellationToken ct);
        Task<string> GetStateAsync(string instanceName, CancellationToken ct);
        Task<GetQrCodeResponse> GetQrCodeAsync(string instanceName, CancellationToken ct);
        Task DisconnectAsync(string instanceName, CancellationToken ct);
        Task DeleteAsync(string instanceName, CancellationToken ct);
    }

    public sealed record CreateInstanceCommand(
        string InstanceName,
        bool GenerateQrCode = true) : IRequest<CreateInstanceResponse>;

    public sealed record CreateInstanceResponse(
        string InstanceName,
        string Status,
        string? QrCodeBase64);

    public sealed class CreateInstanceValidator : IValidator<CreateInstanceCommand>
    {
        public ValidationResult Validate(CreateInstanceCommand cmd)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(cmd.InstanceName))
                result.AddError("InstanceName is required.");

            if (cmd.InstanceName?.Length > 50)
                result.AddError("InstanceName cannot exceed 50 characters.");

            if (cmd.InstanceName is not null && !System.Text.RegularExpressions.Regex.IsMatch(cmd.InstanceName, @"^[a-zA-Z0-9_-]+$"))
                result.AddError("InstanceName can only contain letters, digits, hyphens and underscores.");

            return result;
        }
    }

    public sealed class CreateInstanceHandler(IWhatsAppInstanceService instanceService)
        : IRequestHandler<CreateInstanceCommand, CreateInstanceResponse>
    {
        public Task<CreateInstanceResponse> Handle(CreateInstanceCommand cmd, CancellationToken ct)
            => instanceService.CreateAsync(cmd.InstanceName, cmd.GenerateQrCode, ct);
    }

    // ─── DisconnectInstance ───────────────────────────────────────────────────────



    public sealed record DisconnectInstanceCommand(string InstanceName) : IRequest;

    public sealed class DisconnectInstanceHandler(CreateInstance.IWhatsAppInstanceService instanceService)
        : IRequestHandler<DisconnectInstanceCommand>
    {
        public Task Handle(DisconnectInstanceCommand cmd, CancellationToken ct)
            => instanceService.DisconnectAsync(cmd.InstanceName, ct);
    }

    // ─── GetQrCode ────────────────────────────────────────────────────────────────


    public sealed record GetQrCodeQuery(string InstanceName) : IRequest<GetQrCodeResponse>;

    public sealed record GetQrCodeResponse(
        string InstanceName,
        string? Base64,
        string? PairingCode);

    public sealed class GetQrCodeHandler(CreateInstance.IWhatsAppInstanceService instanceService)
        : IRequestHandler<GetQrCodeQuery, GetQrCodeResponse>
    {
        public Task<GetQrCodeResponse> Handle(GetQrCodeQuery query, CancellationToken ct)
            => instanceService.GetQrCodeAsync(query.InstanceName, ct);
    }
}