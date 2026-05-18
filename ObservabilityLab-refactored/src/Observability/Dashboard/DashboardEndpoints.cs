using System.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Observability.Dashboard
{
    public static class DashboardEndpoints
    {
        public static void MapDashboardEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/dashboard")
                .WithTags("Dashboard");

            group.MapGet("/", GetSnapshot)
                .WithSummary("Live metrics dashboard snapshot");

            group.MapGet("/gc", GetGcStats)
                .WithSummary("Garbage collector statistics");

            group.MapGet("/threads", GetThreadStats)
                .WithSummary("Thread pool statistics");

            group.MapGet("/endpoints", GetEndpointStats)
                .WithSummary("Per-endpoint stats (top 20)");

            group.MapGet("/exceptions", GetRecentExceptions)
                .WithSummary("Last 20 unhandled exceptions");
        }

        private static IResult GetSnapshot(
            DashboardState state,
            AppMetrics metrics)
        {
            var snap = state.GetSnapshot();

            return Results.Ok(snap);
        }

        private static IResult GetGcStats()
        {
            var gcInfo = GC.GetGCMemoryInfo();

            return Results.Ok(new
            {
                gen0Collections = GC.CollectionCount(0),

                gen1Collections = GC.CollectionCount(1),

                gen2Collections = GC.CollectionCount(2),

                totalMemoryBytes = GC.GetTotalMemory(false),

                totalMemoryMb = GC.GetTotalMemory(false) / 1024.0 / 1024.0,

                allocatedBytes = GC.GetTotalAllocatedBytes(),

                heapSizeBytes = gcInfo.HeapSizeBytes,

                fragmentedBytes = gcInfo.FragmentedBytes,

                isServerGc = GCSettings.IsServerGC,

                latencyMode = GCSettings.LatencyMode.ToString(),

                compactionMode =
                    GCSettings.LargeObjectHeapCompactionMode.ToString(),

                timestamp = DateTimeOffset.UtcNow
            });
        }

        private static IResult GetThreadStats()
        {
            ThreadPool.GetAvailableThreads(
                out var availWorker,
                out var availIo);

            ThreadPool.GetMaxThreads(
                out var maxWorker,
                out var maxIo);

            ThreadPool.GetMinThreads(
                out var minWorker,
                out var minIo);

            return Results.Ok(new
            {
                workerThreads = new
                {
                    available = availWorker,
                    max = maxWorker,
                    min = minWorker,
                    inUse = maxWorker - availWorker
                },

                ioThreads = new
                {
                    available = availIo,
                    max = maxIo,
                    min = minIo,
                    inUse = maxIo - availIo
                },

                processThreadCount =
                    System.Diagnostics.Process
                        .GetCurrentProcess()
                        .Threads.Count,

                timestamp = DateTimeOffset.UtcNow
            });
        }

        private static IResult GetEndpointStats(
            DashboardState state)
        {
            var snap = state.GetSnapshot();

            return Results.Ok(snap.TopEndpoints);
        }

        private static IResult GetRecentExceptions(
            DashboardState state)
        {
            var snap = state.GetSnapshot();

            return Results.Ok(snap.RecentExceptions);
        }
    }
}