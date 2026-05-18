using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ObservabilityLab.Api.Endpoints.Diagnostic;

/// <summary>
/// Endpoints de laboratório para simular cenários de observabilidade.
/// NÃO usar em produção.
/// Ideal para:
/// - tracing
/// - métricas
/// - chaos engineering
/// - testes de carga
/// - stress de CPU/memória
/// </summary>
public static class DiagnosticEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/lab")
            .WithTags("Diagnostic Lab");

        // Remova comentário abaixo se estiver usando OpenAPI corretamente
        // .WithOpenApi();

        group.MapGet("/slow", SlowEndpoint)
            .WithSummary("Simula response lento");

        group.MapGet("/error", ErrorEndpoint)
            .WithSummary("Lança exceção deliberada");

        group.MapGet("/cpu-burn", CpuBurnEndpoint)
            .WithSummary("Simula alto consumo de CPU");

        group.MapGet("/memory-pressure", MemoryPressureEndpoint)
            .WithSummary("Simula pressão de memória");

        group.MapPost("/echo", EchoEndpoint)
            .WithSummary("Ecoa payload recebido");

        group.MapGet("/chaos", ChaosEndpoint)
            .WithSummary("Simula falhas aleatórias");

        return app;
    }

    /// <summary>
    /// Simula endpoint lento.
    /// </summary>
    private static async Task<IResult> SlowEndpoint(
        int delayMs = 2000,
        CancellationToken ct = default)
    {
        var clamped = Math.Clamp(delayMs, 100, 30_000);

        await Task.Delay(clamped, ct);

        return Results.Ok(new
        {
            message = "Deliberately slow response",
            delayMs = clamped,
            timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Simula exception não tratada.
    /// </summary>
    private static Task<IResult> ErrorEndpoint(string? message = null)
    {
        throw new InvalidOperationException(
            message ?? "Deliberate test exception from /lab/error");
    }

    /// <summary>
    /// Simula uso intenso de CPU.
    /// </summary>
    private static Task<IResult> CpuBurnEndpoint(
        int durationMs = 1000,
        CancellationToken ct = default)
    {
        var clamped = Math.Clamp(durationMs, 100, 10_000);

        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < clamped)
        {
            if (ct.IsCancellationRequested)
                break;

            _ = Math.Sqrt(
                Random.Shared.NextDouble() * 1_000_000);
        }

        sw.Stop();

        return Task.FromResult<IResult>(
            Results.Ok(new
            {
                message = "CPU burn completed",
                durationMs = clamped,
                elapsedMs = sw.ElapsedMilliseconds
            }));
    }

    /// <summary>
    /// Simula pressão de memória.
    /// </summary>
    private static Task<IResult> MemoryPressureEndpoint(
        int sizeMb = 50)
    {
        var clamped = Math.Clamp(sizeMb, 1, 500);

        var buffer = new byte[clamped * 1024 * 1024];

        Random.Shared.NextBytes(buffer);

        GC.Collect(2, GCCollectionMode.Forced);

        return Task.FromResult<IResult>(
            Results.Ok(new
            {
                message = "Memory pressure test complete",
                allocatedMb = clamped,
                gcGen0 = GC.CollectionCount(0),
                gcGen1 = GC.CollectionCount(1),
                gcGen2 = GC.CollectionCount(2),
                totalMemoryMb =
                    GC.GetTotalMemory(false) / 1024.0 / 1024.0
            }));
    }

    /// <summary>
    /// Ecoa payload recebido.
    /// Útil para tracing/correlation.
    /// </summary>
    private static async Task<IResult> EchoEndpoint(
        JsonElement body,
        HttpContext ctx,
        CancellationToken ct)
    {
        await Task.Delay(
            Random.Shared.Next(10, 100),
            ct);

        return Results.Ok(new
        {
            echo = body,
            receivedAt = DateTimeOffset.UtcNow,
            traceId = Activity.Current?.TraceId.ToString(),
            spanId = Activity.Current?.SpanId.ToString(),
            correlationId = ctx.Items["CorrelationId"],
            remoteIp = ctx.Connection.RemoteIpAddress?.ToString(),
            userAgent = ctx.Request.Headers.UserAgent.ToString()
        });
    }

    /// <summary>
    /// Chaos engineering endpoint.
    /// Simula comportamentos aleatórios.
    /// </summary>
    private static async Task<IResult> ChaosEndpoint(
        CancellationToken ct)
    {
        var mode = Random.Shared.Next(0, 4);

        switch (mode)
        {
            case 0:
                return Results.Ok(new
                {
                    chaos = "normal",
                    note = "Nothing happened"
                });

            case 1:
                {
                    var delay =
                        Random.Shared.Next(1000, 5000);

                    await Task.Delay(delay, ct);

                    return Results.Ok(new
                    {
                        chaos = "slow",
                        delay
                    });
                }

            case 2:
                throw new Exception(
                    $"Chaos monkey struck at {DateTimeOffset.UtcNow}");

            case 3:
                return Results.Accepted(
                    "/api/v1/lab/chaos",
                    new
                    {
                        chaos = "partial",
                        note = "Processing queued"
                    });

            default:
                return Results.Ok(new
                {
                    chaos = "unknown"
                });
        }
    }
}