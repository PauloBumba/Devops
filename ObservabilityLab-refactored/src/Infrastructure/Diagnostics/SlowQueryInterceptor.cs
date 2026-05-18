using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Infrastructure.Diagnostics;

/// <summary>
/// Intercepta queries do EF Core: detecta queries lentas e possíveis N+1.
/// Responsabilidade única: análise e instrumentação de acesso ao banco.
/// </summary>
public sealed class SlowQueryInterceptor(
    AppMetrics     metrics,
    ILogger<SlowQueryInterceptor> logger,
    IConfiguration config) : DbCommandInterceptor
{
    private readonly double _slowThresholdMs =
        config.GetValue<double>("Observability:SlowQueryThresholdMs", 300);

    private static readonly ConcurrentDictionary<string, List<string>> QueryHistory = new();

    public override async ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(
        System.Data.Common.DbCommand         command,
        CommandExecutedEventData eventData,
        System.Data.Common.DbDataReader result,
        CancellationToken ct = default)
    {
        var elapsed = eventData.Duration.TotalMilliseconds;
        var sql     = command.CommandText;
        var traceId = Activity.Current?.TraceId.ToString() ?? "no-trace";

        metrics.DbQueryDuration.Record(elapsed, new TagList
        {
            { "db.operation", ExtractOperation(sql) },
            { "db.table",     ExtractTable(sql) }
        });

        if (elapsed >= _slowThresholdMs)
            logger.LogWarning("SLOW QUERY [{Elapsed:F0}ms] TraceId: {TraceId}\n{Sql}",
                elapsed, traceId, TruncateSql(sql, 500));

        DetectNPlusOne(sql, traceId);

        return result;
    }

    private void DetectNPlusOne(string sql, string traceId)
    {
        if (string.IsNullOrEmpty(traceId)) return;

        var normalized = NormalizeSql(sql);
        var history    = QueryHistory.GetOrAdd(traceId, _ => []);

        lock (history)
        {
            history.Add(normalized);
            var count = history.Count(q => q == normalized);

            if (count == 5)
                logger.LogWarning(
                    "Possible N+1 detected: query executed {Count}x in trace {TraceId}\n{Sql}",
                    count, traceId, TruncateSql(sql, 300));
        }
    }

    private static string ExtractOperation(string sql)
    {
        var trimmed = sql.TrimStart();
        return trimmed.Length >= 6 ? trimmed[..6].ToUpperInvariant() : "UNKNOWN";
    }

    private static string ExtractTable(string sql)
    {
        foreach (var pattern in new[] { " FROM ", " INTO ", " UPDATE ", " JOIN " })
        {
            var idx = sql.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var rest = sql[(idx + pattern.Length)..].TrimStart('"', '[', '`');
            var end  = rest.IndexOfAny([' ', '\n', '\r', '"', ']', '`', '(']);
            return end > 0 ? rest[..end] : rest[..Math.Min(30, rest.Length)];
        }
        return "unknown";
    }

    private static string NormalizeSql(string sql)
        => Regex.Replace(sql, @"@p\d+", "@p");

    private static string TruncateSql(string sql, int max)
        => sql.Length <= max ? sql : sql[..max] + "... [truncated]";
}
