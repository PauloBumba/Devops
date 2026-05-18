using System.Diagnostics;
using System.Text.Json;

namespace ObservabilityLab.Api.Endpoints.Diagnostic;

/// <summary>
/// Endpoints de laboratório para simular cenários de observabilidade.
/// NÃO usar em produção — apenas para fins de diagnóstico e load testing.
/// </summary>
public static class DiagnosticEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/lab")
            .WithTags("Diagnostic Lab")
            .WithOpenApi();

        group.MapGet("/slow",             SlowEndpoint)
            .WithSummary("Simula response lento (ms configurável)");

        group.MapGet("/error",            ErrorEndpoint)
            .WithSummary("Lança exceção deliberadamente");

        group.MapGet("/cpu-burn",         CpuBurnEndpoint)
            .WithSummary("Simula CPU-bound intenso");

        group.MapGet("/memory-pressure",  MemoryPressureEndpoint)
            .WithSummary("Aloca buffer para simular pressão de memória");

        group.MapPost("/echo",            EchoEndpoint)
            .WithSummary("Ecoa body com metadados de request");

        group.MapGet("/chaos",            ChaosEndpoint)
            .WithSummary("Injeta falhas/atrasos aleatórios (chaos engineering)");

        return app;
    }

    private static async Task<IResult> SlowEndpoint(int delayMs = 2000, CancellationToken ct = default)
    {
        var clamped = Math.Clamp(delayMs, 100, 30_000);
        await Task.Delay(clamped, ct);
        return Results.Ok(new { message = "Deliberately slow response", delayMs = clamped, timestamp = DateTimeOffset.UtcNow });
    }

    private static Task<IResult> ErrorEndpoint(string? message = null)
        => throw new InvalidOperationException(message ?? "Deliberate test exception from /lab/error");

    private static Task<IResult> CpuBurnEndpoint(int durationMs = 1000, CancellationToken ct = default)
    {
        var clamped = Math.Clamp(durationMs, 100, 10_000);
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < clamped)
        {
            if (ct.IsCancellationRequested) break;
            Math.Sqrt(Random.Shared.NextDouble() * 1_000_000);
        }
        return Task.FromResult<IResult>(Results.Ok(new
        {
            message   = "CPU burn completed",
            durationMs = clamped,
            elapsed   = sw.ElapsedMilliseconds
        }));
    }

    private static Task<IResult> MemoryPressureEndpoint(int sizeMb = 50)
    {
        var clamped = Math.Clamp(sizeMb, 1, 500);
        var buffer  = new byte[clamped * 1024 * 1024];
        Random.Shared.NextBytes(buffer);
        GC.Collect(2, GCCollectionMode.Forced);
        return Task.FromResult<IResult>(Results.Ok(new
        {
            message     = "Memory pressure test complete",
            allocatedMb = clamped,
            gcGen0      = GC.CollectionCount(0),
            gcGen1      = GC.CollectionCount(1),
            gcGen2      = GC.CollectionCount(2)
        }));
    }

    private static async Task<IResult> EchoEndpoint(
        JsonElement       body,
        HttpContext       ctx,
        CancellationToken ct)
    {
        await Task.Delay(Random.Shared.Next(10, 100), ct);
        return Results.Ok(new
        {
            echo          = body,
            receivedAt    = DateTimeOffset.UtcNow,
            correlationId = ctx.Items["CorrelationId"],
            remoteIp      = ctx.Connection.RemoteIpAddress?.ToString()
        });
    }

    private static async Task<IResult> ChaosEndpoint(CancellationToken ct)
        => Random.Shared.Next(0, 4) switch
        {
            0 => Results.Ok(new { chaos = "normal", note = "Nothing happened" }),
            1 => await (async () =>
            {
                var d = Random.Shared.Next(1000, 5000);
                await Task.Delay(d, ct);
                return Results.Ok(new { chaos = "slow", delay = d });
            })(),
            2 => throw new Exception($"Chaos monkey struck at {DateTimeOffset.UtcNow}"),
            3 => Results.Accepted("/api/v1/lab/chaos", new { chaos = "partial", note = "Processing queued" }),
            _ => Results.Ok(new { chaos = "unknown" })
        };
}
