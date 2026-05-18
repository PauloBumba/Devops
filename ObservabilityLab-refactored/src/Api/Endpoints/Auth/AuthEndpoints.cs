using MediatR;
using ObservabilityLab.Application.Features.Auth.Login;
using ObservabilityLab.Observability.Metrics;
using System.Diagnostics;

namespace ObservabilityLab.Api.Endpoints.Auth;

/// <summary>
/// Endpoints de autenticação. Delega toda lógica ao LoginHandler via MediatR.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/login", Login)
            .WithTags("Auth")
            .WithName("Login")
            .WithSummary("Autenticação — retorna token JWT-like")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> Login(
        LoginRequest      req,
        IMediator         mediator,
        AppMetrics        metrics,
        AppDiagnostics    diagnostics,
        ILogger<Program>  logger,
        CancellationToken ct)
    {
        using var activity = diagnostics.StartBusinessActivity("auth.login");
        activity?.SetTag("auth.username", req.Username);

        metrics.LoginAttempts.Add(1, new TagList { { "auth.username", req.Username } });

        var result = await mediator.Send(new LoginCommand(req.Username, req.Password), ct);

        if (!result.Success)
        {
            metrics.LoginFailures.Add(1, new TagList { { "auth.username", req.Username } });
            activity?.SetTag("auth.success", false);
            logger.LogWarning("Failed login attempt for user: {Username}", req.Username);
            return Results.Unauthorized();
        }

        activity?.SetTag("auth.success", true);
        logger.LogInformation("Successful login for user: {Username}", req.Username);

        return Results.Ok(new
        {
            token     = result.Token,
            expiresIn = 3600,
            username  = result.Username
        });
    }
}

/// <summary>Request body de login.</summary>
public sealed record LoginRequest(string Username, string Password);
