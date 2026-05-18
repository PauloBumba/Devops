using MediatR;
using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Application.Common;

namespace ObservabilityLab.Application.Features.Auth.Login;

// ─── Command ──────────────────────────────────────────────────────────────────

public sealed record LoginCommand(string Username, string Password) : IRequest<LoginResult>;

// ─── Result ───────────────────────────────────────────────────────────────────

public sealed record LoginResult(bool Success, string? Token, string? Username);

// ─── Validator ────────────────────────────────────────────────────────────────

public sealed class LoginCommandValidator : IValidator<LoginCommand>
{
    public ValidationResult Validate(LoginCommand cmd)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(cmd.Username))
            result.AddError("Username is required.");

        if (cmd.Username?.Length > 100)
            result.AddError("Username must be at most 100 characters.");

        if (string.IsNullOrWhiteSpace(cmd.Password))
            result.AddError("Password is required.");

        return result;
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────

/// <summary>
/// Handler de autenticação. Em produção, substituir por verificação no banco
/// e hashing de senha (ex: BCrypt ou ASP.NET Core Identity).
/// </summary>
public sealed class LoginHandler : IRequestHandler<LoginCommand, LoginResult>
{
    // Em produção: buscar usuário no banco com hash de senha
    private static readonly IReadOnlyDictionary<string, string> KnownUsers =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"]  = "admin123",
            ["user"]   = "user123",
            ["tester"] = "tester123"
        };

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken ct)
    {
        // Simula latência de autenticação real
        await Task.Delay(Random.Shared.Next(50, 200), ct);

        if (!KnownUsers.TryGetValue(request.Username, out var expected) || expected != request.Password)
            return new LoginResult(false, null, null);

        var token = GenerateToken(request.Username);
        return new LoginResult(true, token, request.Username);
    }

    private static string GenerateToken(string username)
    {
        // Em produção: usar JWT com chave assimétrica (RS256)
        var payload = $"{username}:{DateTimeOffset.UtcNow.Ticks}:{Guid.NewGuid()}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
    }
}
