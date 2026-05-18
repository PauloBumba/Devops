using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ObservabilityLab.Observability.Metrics;

/// <summary>
/// Central metrics registry for the application.
/// Uses System.Diagnostics.Metrics (OTel-compatible).
/// </summary>
public sealed class AppMetrics : IDisposable
{
    public const string MeterName = "ObservabilityLab";

    private readonly Meter _meter;

    // ── Counters ──────────────────────────────────────────────────────────────
    public readonly Counter<long> RequestCount;
    public readonly Counter<long> ErrorCount;
    public readonly Counter<long> LoginAttempts;
    public readonly Counter<long> LoginFailures;
    public readonly Counter<long> SlowRequests;

    // ── Histograms (latency) ──────────────────────────────────────────────────
    public readonly Histogram<double> RequestDuration;
    public readonly Histogram<double> DbQueryDuration;
    public readonly Histogram<double> CacheDuration;

    // ── MediatR pipeline metrics ──────────────────────────────────────────────
    public readonly Histogram<double> MediatRDuration;
    public readonly Counter<long>     MediatRErrors;

    // ── Gauges (in-memory snapshots) ──────────────────────────────────────────
    private long   _activeRequests;
    private double _cpuUsage;
    private double _memoryUsageMb;
    private double _avgResponseTimeMs;
    private long   _totalErrors;
    private long   _totalRequests;

    // ── Per-endpoint P99/P95/P50 tracking ────────────────────────────────────
    private readonly ConcurrentDictionary<string, List<double>> _endpointLatencies = new();
    private readonly object _latencyLock = new();

    public AppMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        RequestCount = _meter.CreateCounter<long>(
            "app.requests.total",
            unit: "{requests}",
            description: "Total number of HTTP requests received");

        ErrorCount = _meter.CreateCounter<long>(
            "app.errors.total",
            unit: "{errors}",
            description: "Total number of errors");

        LoginAttempts = _meter.CreateCounter<long>(
            "app.auth.login_attempts",
            unit: "{attempts}",
            description: "Total login attempts");

        LoginFailures = _meter.CreateCounter<long>(
            "app.auth.login_failures",
            unit: "{failures}",
            description: "Total login failures");

        SlowRequests = _meter.CreateCounter<long>(
            "app.requests.slow",
            unit: "{requests}",
            description: "Requests exceeding slow threshold");

        RequestDuration = _meter.CreateHistogram<double>(
            "app.request.duration",
            unit: "ms",
            description: "HTTP request duration in milliseconds");

        DbQueryDuration = _meter.CreateHistogram<double>(
            "app.db.query_duration",
            unit: "ms",
            description: "Database query duration in milliseconds");

        CacheDuration = _meter.CreateHistogram<double>(
            "app.cache.operation_duration",
            unit: "ms",
            description: "Cache operation duration in milliseconds");

        MediatRDuration = _meter.CreateHistogram<double>(
            "app.mediatr.handler_duration",
            unit: "ms",
            description: "MediatR handler execution duration in milliseconds");

        MediatRErrors = _meter.CreateCounter<long>(
            "app.mediatr.errors_total",
            unit: "{errors}",
            description: "Total MediatR handler errors");

        // Observable gauges that pull from in-memory state
        _meter.CreateObservableGauge(
            "app.requests.active",
            () => _activeRequests,
            unit: "{requests}",
            description: "Currently in-flight HTTP requests");

        _meter.CreateObservableGauge(
            "system.cpu.usage_percent",
            () => _cpuUsage,
            unit: "%",
            description: "Process CPU usage percentage");

        _meter.CreateObservableGauge(
            "system.memory.used_mb",
            () => _memoryUsageMb,
            unit: "MB",
            description: "Process memory usage in MB");

        _meter.CreateObservableGauge(
            "app.request.avg_duration_ms",
            () => _avgResponseTimeMs,
            unit: "ms",
            description: "Rolling average request duration");
    }

    // ── Mutation helpers ──────────────────────────────────────────────────────

    public void IncrementActiveRequests() => Interlocked.Increment(ref _activeRequests);
    public void DecrementActiveRequests() => Interlocked.Decrement(ref _activeRequests);
    public long GetActiveRequests()       => Interlocked.Read(ref _activeRequests);

    public void UpdateSystemMetrics(double cpuPercent, double memoryMb)
    {
        _cpuUsage      = cpuPercent;
        _memoryUsageMb = memoryMb;
    }

    public void RecordRequestCompleted(string endpoint, string method, int statusCode, double durationMs)
    {
        var tags = new TagList
        {
            { "http.route",        endpoint },
            { "http.method",       method },
            { "http.status_code",  statusCode },
            { "http.status_class", $"{statusCode / 100}xx" }
        };

        RequestCount.Add(1, tags);
        RequestDuration.Record(durationMs, tags);

        Interlocked.Increment(ref _totalRequests);
        UpdateRollingAverage(durationMs);
        TrackEndpointLatency(endpoint, durationMs);

        if (statusCode >= 500)
        {
            ErrorCount.Add(1, tags);
            Interlocked.Increment(ref _totalErrors);
        }
    }

    public double GetErrorRate()
    {
        var total = Interlocked.Read(ref _totalRequests);
        return total == 0 ? 0 : (double)Interlocked.Read(ref _totalErrors) / total * 100;
    }

    public LatencyPercentiles GetEndpointPercentiles(string endpoint)
    {
        lock (_latencyLock)
        {
            if (!_endpointLatencies.TryGetValue(endpoint, out var values) || values.Count == 0)
                return new LatencyPercentiles();

            var sorted = values.OrderBy(v => v).ToArray();
            return new LatencyPercentiles
            {
                P50 = Percentile(sorted, 50),
                P95 = Percentile(sorted, 95),
                P99 = Percentile(sorted, 99),
                Max = sorted[^1],
                Min = sorted[0]
            };
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void UpdateRollingAverage(double durationMs)
    {
        // Exponential moving average (α = 0.1)
        _avgResponseTimeMs = _avgResponseTimeMs == 0
            ? durationMs
            : 0.9 * _avgResponseTimeMs + 0.1 * durationMs;
    }

    private void TrackEndpointLatency(string endpoint, double durationMs)
    {
        lock (_latencyLock)
        {
            var list = _endpointLatencies.GetOrAdd(endpoint, _ => new List<double>());
            list.Add(durationMs);
            // Keep last 1000 samples per endpoint
            if (list.Count > 1000) list.RemoveAt(0);
        }
    }

    private static double Percentile(double[] sorted, int percentile)
    {
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    public void Dispose() => _meter.Dispose();
}

public record LatencyPercentiles
{
    public double P50 { get; init; }
    public double P95 { get; init; }
    public double P99 { get; init; }
    public double Max { get; init; }
    public double Min { get; init; }
}
