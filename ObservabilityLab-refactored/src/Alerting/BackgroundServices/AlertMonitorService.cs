using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Alerting.Abstractions;
using ObservabilityLab.Alerting.Services;
using ObservabilityLab.Observability.Dashboard;

namespace ObservabilityLab.Alerting.BackgroundServices
{


    public sealed class AlertMonitorService(
        DashboardState dashboard,
        IEnumerable<IAlertPolicy> policies,
        AlertService alertService,
        ILogger<AlertMonitorService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            logger.LogInformation("{Service} started", nameof(AlertMonitorService));

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), ct);
                    await EvaluatePoliciesAsync(ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{Service} evaluation error", nameof(AlertMonitorService));
                }
            }
        }

        private async Task EvaluatePoliciesAsync(CancellationToken ct)
        {
            var snapshot = dashboard.GetSnapshot();

            foreach (var policy in policies)
            {
                var alerts = policy.Evaluate(snapshot);

                foreach (var alert in alerts)
                {
                    logger.LogWarning(
                        "Policy {Policy} triggered: {AlertTitle} [{Severity}]",
                        policy.Name, alert.Title, alert.Severity);

                    var result = await alertService.SendWithFallbackAsync(alert, ct);

                    if (!result.Delivered)
                        logger.LogError(
                            "ALERT NOT DELIVERED: {AlertTitle} — All channels failed",
                            alert.Title);
                }
            }
        }
    }
}