using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Infrastructure.Cache;

/// <summary>
/// Cache em duas camadas: L1 (in-process MemoryCache) + L2 (Redis distribuído).
/// Se Redis falhar → cai para L1. Se L1 miss → busca L2.
/// Instrumentado com métricas OTel e distributed tracing.
/// </summary>
public sealed class TwoLayerCacheService(
    IMemoryCache l1,
    IDistributedCache l2,
    AppMetrics metrics,
    AppDiagnostics diagnostics,
    ILogger<TwoLayerCacheService> logger)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // ── Get ───────────────────────────────────────────────────────────────────

    public async Task<(T? Value, CacheHitLevel HitLevel)> GetAsync<T>(
        string key,
        CancellationToken ct = default)
    {
        using var activity = diagnostics.StartCacheActivity("GET", key);

        // L1 — MemoryCache (nanoseconds)
        if (l1.TryGetValue(key, out T? l1Value))
        {
            RecordHit("L1", key);
            activity?.SetTag("cache.level", "L1");
            return (l1Value, CacheHitLevel.L1);
        }

        // L2 — Redis (microseconds)
        var sw = Stopwatch.StartNew();
        try
        {
            var raw = await l2.GetStringAsync(key, ct);
            sw.Stop();

            metrics.CacheDuration.Record(sw.Elapsed.TotalMilliseconds,
                new TagList { { "cache.operation", "GET" }, { "cache.level", "L2" } });

            if (raw is not null)
            {
                var value = JsonSerializer.Deserialize<T>(raw, _json);
                // Promove para L1 com TTL curto (hot path local)
                l1.Set(key, value, TimeSpan.FromSeconds(30));

                RecordHit("L2", key);
                activity?.SetTag("cache.level", "L2");
                return (value, CacheHitLevel.L2);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis GET failed for key '{Key}' — falling back to miss", key);
            AppDiagnostics.RecordException(activity, ex);
        }

        RecordMiss(key);
        activity?.SetTag("cache.hit", false);
        return (default, CacheHitLevel.Miss);
    }

    // ── Set ───────────────────────────────────────────────────────────────────

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiry = null,
        CancellationToken ct = default)
    {
        using var activity = diagnostics.StartCacheActivity("SET", key);
        var ttl = absoluteExpiry ?? TimeSpan.FromMinutes(5);

        // L1 — sempre
        l1.Set(key, value, ttl);

        // L2 — Redis (best-effort; não quebra se cair)
        var sw = Stopwatch.StartNew();
        try
        {
            var json = JsonSerializer.Serialize(value, _json);
            await l2.SetStringAsync(key, json,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
            sw.Stop();

            metrics.CacheDuration.Record(sw.Elapsed.TotalMilliseconds,
                new TagList { { "cache.operation", "SET" }, { "cache.level", "L2" } });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis SET failed for key '{Key}' — L1 only", key);
        }
    }

    // ── Invalidate ────────────────────────────────────────────────────────────

    public async Task InvalidateAsync(string key, CancellationToken ct = default)
    {
        l1.Remove(key);
        try { await l2.RemoveAsync(key, ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis REMOVE failed for key '{Key}'", key);
        }
    }

    // ── Get-Or-Set (cache-aside) ───────────────────────────────────────────────

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiry = null,
        CancellationToken ct = default)
    {
        var (cached, level) = await GetAsync<T>(key, ct);
        if (level != CacheHitLevel.Miss && cached is not null)
            return cached;

        logger.LogDebug("Cache miss for '{Key}' — calling factory", key);
        var value = await factory(ct);
        await SetAsync(key, value, absoluteExpiry, ct);
        return value;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RecordHit(string level, string key)
    {
        logger.LogDebug("Cache HIT [{Level}] — {Key}", level, key);
        metrics.CacheDuration.Record(0,
            new TagList { { "cache.hit", true }, { "cache.level", level } });
    }

    private void RecordMiss(string key)
    {
        logger.LogDebug("Cache MISS — {Key}", key);
        metrics.CacheDuration.Record(0,
            new TagList { { "cache.hit", false } });
    }
}

public enum CacheHitLevel { Miss, L1, L2 }
