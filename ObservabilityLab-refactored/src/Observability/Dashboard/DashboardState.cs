using System.Collections.Concurrent;

namespace ObservabilityLab.Observability.Dashboard
{

    /// <summary>
    /// Thread-safe, in-memory store for live dashboard data.
    /// Designed to be read frequently — all reads are O(1).
    /// </summary>
    public sealed class DashboardState
    {
        // ── Concurrency ───────────────────────────────────────────────────────────
        private long _concurrentUsers;
        private long _totalRequests;
        private long _totalErrors;
        private double _avgResponseTimeMs;

        // ── System ────────────────────────────────────────────────────────────────
        public double CpuUsagePercent { get; private set; }
        public double MemoryUsageMb { get; private set; }
        public double MemoryUsagePercent { get; private set; }
        public double ThreadPoolAvail { get; private set; }

        // ── Recent request window (last 60 seconds, per-second buckets) ───────────
        private readonly ConcurrentQueue<RequestEntry> _recentRequests = new();
        private readonly ConcurrentQueue<SlowRequestEntry> _slowRequests = new();
        private readonly ConcurrentQueue<ExceptionEntry> _recentExceptions = new();

        // ── Per-endpoint stats ────────────────────────────────────────────────────
        private readonly ConcurrentDictionary<string, EndpointStats> _endpointStats = new();

        // ── Public counters ───────────────────────────────────────────────────────
        public long ConcurrentUsers => Interlocked.Read(ref _concurrentUsers);
        public long TotalRequests => Interlocked.Read(ref _totalRequests);
        public long TotalErrors => Interlocked.Read(ref _totalErrors);
        public double AvgResponseMs => _avgResponseTimeMs;
        public double ErrorRatePercent
            => TotalRequests == 0 ? 0 : (double)TotalErrors / TotalRequests * 100;

        public double RequestsPerSecond
        {
            get
            {
                var cutoff = DateTimeOffset.UtcNow.AddSeconds(-1);
                return _recentRequests.Count(r => r.Timestamp >= cutoff);
            }
        }

        // ── Mutations ─────────────────────────────────────────────────────────────

        public void IncrementConcurrentUsers() => Interlocked.Increment(ref _concurrentUsers);
        public void DecrementConcurrentUsers() => Interlocked.Decrement(ref _concurrentUsers);

        public void RecordRequest(string route, string method, int statusCode, double durationMs)
        {
            Interlocked.Increment(ref _totalRequests);

            if (statusCode >= 500) Interlocked.Increment(ref _totalErrors);

            // Exponential moving average
            _avgResponseTimeMs = _avgResponseTimeMs == 0
                ? durationMs
                : 0.9 * _avgResponseTimeMs + 0.1 * durationMs;

            _recentRequests.Enqueue(new RequestEntry(route, method, statusCode, durationMs, DateTimeOffset.UtcNow));
            TrimQueue(_recentRequests, 5000);

            // Per-endpoint stats
            _endpointStats.AddOrUpdate(
                route,
                _ => new EndpointStats { Route = route, Count = 1, TotalMs = durationMs, Errors = statusCode >= 500 ? 1 : 0 },
                (_, existing) =>
                {
                    existing.Count++;
                    existing.TotalMs += durationMs;
                    if (statusCode >= 500) existing.Errors++;
                    return existing;
                });
        }

        public void RecordSlowRequest(string route, string method, double durationMs)
        {
            _slowRequests.Enqueue(new SlowRequestEntry(route, method, durationMs, DateTimeOffset.UtcNow));
            TrimQueue(_slowRequests, 100);
        }

        public void RecordException(Exception ex, string route)
        {
            _recentExceptions.Enqueue(new ExceptionEntry(
                ex.GetType().Name,
                ex.Message,
                route,
                DateTimeOffset.UtcNow));
            TrimQueue(_recentExceptions, 100);
        }

        public void UpdateSystemMetrics(double cpu, double memMb, double memPercent)
        {
            CpuUsagePercent = cpu;
            MemoryUsageMb = memMb;
            MemoryUsagePercent = memPercent;

            ThreadPool.GetAvailableThreads(out var workerThreads, out _);
            ThreadPoolAvail = workerThreads;
        }

        // ── Read projections ──────────────────────────────────────────────────────

        public DashboardSnapshot GetSnapshot() => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            ConcurrentUsers = ConcurrentUsers,
            RequestsPerSecond = RequestsPerSecond,
            TotalRequests = TotalRequests,
            TotalErrors = TotalErrors,
            ErrorRatePercent = ErrorRatePercent,
            AvgResponseMs = AvgResponseMs,
            CpuUsagePercent = CpuUsagePercent,
            MemoryUsageMb = MemoryUsageMb,
            MemoryUsagePercent = MemoryUsagePercent,
            ThreadPoolAvailable = ThreadPoolAvail,
            RecentExceptions = _recentExceptions.TakeLast(20).ToList(),
            SlowRequests = _slowRequests.TakeLast(20).ToList(),
            TopEndpoints = _endpointStats.Values
                                        .OrderByDescending(e => e.Count)
                                        .Take(10)
                                        .Select(e => new EndpointSummary
                                        {
                                            Route = e.Route,
                                            Count = e.Count,
                                            AvgMs = e.Count == 0 ? 0 : e.TotalMs / e.Count,
                                            ErrorRate = e.Count == 0 ? 0 : (double)e.Errors / e.Count * 100
                                        })
                                        .ToList()
        };

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void TrimQueue<T>(ConcurrentQueue<T> queue, int maxSize)
        {
            while (queue.Count > maxSize) queue.TryDequeue(out _);
        }
    }

    // ── Records & models ──────────────────────────────────────────────────────────

    public record RequestEntry(string Route, string Method, int StatusCode, double DurationMs, DateTimeOffset Timestamp);
    public record SlowRequestEntry(string Route, string Method, double DurationMs, DateTimeOffset Timestamp);
    public record ExceptionEntry(string Type, string Message, string Route, DateTimeOffset Timestamp);

    public class EndpointStats
    {
        public string Route { get; set; } = "";
        public long Count { get; set; }
        public double TotalMs { get; set; }
        public long Errors { get; set; }
    }

    public class DashboardSnapshot
    {
        public DateTimeOffset Timestamp { get; init; }
        public long ConcurrentUsers { get; init; }
        public double RequestsPerSecond { get; init; }
        public long TotalRequests { get; init; }
        public long TotalErrors { get; init; }
        public double ErrorRatePercent { get; init; }
        public double AvgResponseMs { get; init; }
        public double CpuUsagePercent { get; init; }
        public double MemoryUsageMb { get; init; }
        public double MemoryUsagePercent { get; init; }
        public double ThreadPoolAvailable { get; init; }
        public List<ExceptionEntry> RecentExceptions { get; init; } = [];
        public List<SlowRequestEntry> SlowRequests { get; init; } = [];
        public List<EndpointSummary> TopEndpoints { get; init; } = [];
    }

    public class EndpointSummary
    {
        public string Route { get; set; } = "";
        public long Count { get; set; }
        public double AvgMs { get; set; }
        public double ErrorRate { get; set; }
    }
}