using System.Diagnostics;

namespace ObservabilityLab.Observability.Metrics;

/// <summary>
/// Provides the ActivitySource for distributed tracing across the application.
/// Create child spans to track cross-cutting operations.
/// </summary>
public sealed class AppDiagnostics
{
    public const string ActivitySourceName = "ObservabilityLab";

    private static readonly ActivitySource _source = new(ActivitySourceName, "1.0.0");

    // ── Convenience factories ──────────────────────────────────────────────────

    public Activity? StartDbActivity(string operation, string table)
    {
        var activity = _source.StartActivity($"db.{operation}", ActivityKind.Client);
        activity?.SetTag("db.system",    "postgresql");
        activity?.SetTag("db.operation", operation);
        activity?.SetTag("db.table",     table);
        return activity;
    }

    public Activity? StartCacheActivity(string operation, string key)
    {
        var activity = _source.StartActivity($"cache.{operation}", ActivityKind.Client);
        activity?.SetTag("cache.system",    "redis");
        activity?.SetTag("cache.operation", operation);
        activity?.SetTag("cache.key",       key);
        return activity;
    }

    public Activity? StartBusinessActivity(string name)
    {
        var activity = _source.StartActivity(name, ActivityKind.Internal);
        return activity;
    }

    public Activity? StartAlertActivity(string channel)
    {
        var activity = _source.StartActivity("alert.send", ActivityKind.Producer);
        activity?.SetTag("alert.channel", channel);
        return activity;
    }

    // ── Enrichment helpers ────────────────────────────────────────────────────

    public static void RecordException(Activity? activity, Exception ex)
    {
        if (activity is null) return;
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.RecordException(ex, new TagList
        {
            { "exception.escaped", true }
        });
    }

    public static string GetTraceId()
        => Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

    public static string? GetSpanId()
        => Activity.Current?.SpanId.ToString();
}
