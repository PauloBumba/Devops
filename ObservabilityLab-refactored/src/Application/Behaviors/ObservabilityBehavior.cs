using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Observability.Metrics;

namespace ObservabilityLab.Application.Behaviors
{


    public sealed class ObservabilityBehavior<TRequest, TResponse>(
        AppMetrics metrics,
        AppDiagnostics diagnostics,
        ILogger<ObservabilityBehavior<TRequest, TResponse>> logger)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct)
        {
            var requestName = typeof(TRequest).Name;
            var kind = requestName.EndsWith("Command", StringComparison.OrdinalIgnoreCase)
                ? "command" : "query";

            using var activity = diagnostics.StartBusinessActivity($"{kind}.{requestName}");
            activity?.SetTag("mediatr.request_type", requestName);
            activity?.SetTag("mediatr.kind", kind);

            var sw = Stopwatch.StartNew();
            logger.LogDebug("→ MediatR {Kind} {RequestName}", kind, requestName);

            try
            {
                var response = await next();
                sw.Stop();

                activity?.SetTag("mediatr.success", true);
                activity?.SetTag("mediatr.duration_ms", sw.Elapsed.TotalMilliseconds);

                metrics.MediatRDuration.Record(sw.Elapsed.TotalMilliseconds, new TagList
            {
                { "mediatr.request", requestName },
                { "mediatr.kind",    kind },
                { "mediatr.success", "true" }
            });

                logger.LogDebug("✓ MediatR {RequestName} completed in {ElapsedMs:F1}ms", requestName, sw.Elapsed.TotalMilliseconds);
                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                AppDiagnostics.RecordException(activity, ex);

                metrics.MediatRDuration.Record(sw.Elapsed.TotalMilliseconds, new TagList
            {
                { "mediatr.request", requestName },
                { "mediatr.kind",    kind },
                { "mediatr.success", "false" }
            });

                metrics.MediatRErrors.Add(1, new TagList
            {
                { "mediatr.request",        requestName },
                { "mediatr.exception_type", ex.GetType().Name }
            });

                logger.LogError(ex, "✗ MediatR {RequestName} failed after {ElapsedMs:F1}ms", requestName, sw.Elapsed.TotalMilliseconds);
                throw;
            }
        }
    }
}