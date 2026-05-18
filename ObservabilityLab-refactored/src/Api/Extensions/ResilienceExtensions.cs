using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using ObservabilityLab.Infrastructure.Integrations.WhatsApp.EvolutionApi.Options;

namespace ObservabilityLab.Api.Extensions;

public static class ResilienceExtensions
{
    /// <summary>
    /// Resiliência específica para Evolution API.
    /// Retry + Circuit Breaker + Timeout.
    /// </summary>
    public static IHttpClientBuilder AddEvolutionApiResilience(
        this IHttpClientBuilder builder,
        EvolutionApiOptions options)
    {
        builder.AddStandardResilienceHandler(opts =>
        {
            opts.Retry = new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = options.RetryCount,

                Delay = TimeSpan.FromMilliseconds(300),

                BackoffType = DelayBackoffType.Exponential,

                UseJitter = true,

                ShouldHandle = args =>
                    ValueTask.FromResult(

                        args.Outcome.Exception is HttpRequestException ||

                        args.Outcome.Result?.StatusCode is
                            >= System.Net.HttpStatusCode.InternalServerError

                            or System.Net.HttpStatusCode.TooManyRequests)
            };

            opts.CircuitBreaker = new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(30),

                FailureRatio =
                    options.CircuitBreaker.FailureRatio,

                MinimumThroughput =
                    options.CircuitBreaker.FailureThreshold,

                BreakDuration = TimeSpan.FromSeconds(
                    options.CircuitBreaker.DurationSeconds),

                OnOpened = args =>
                {
                    Console.WriteLine(
                        $"[EvolutionApi CircuitBreaker] OPEN for {args.BreakDuration.TotalSeconds}s");

                    return ValueTask.CompletedTask;
                },

                OnClosed = _ =>
                {
                    Console.WriteLine(
                        "[EvolutionApi CircuitBreaker] CLOSED");

                    return ValueTask.CompletedTask;
                },

                OnHalfOpened = _ =>
                {
                    Console.WriteLine(
                        "[EvolutionApi CircuitBreaker] HALF-OPEN");

                    return ValueTask.CompletedTask;
                }
            };

            opts.AttemptTimeout =
                new HttpTimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(
                        options.TimeoutSeconds)
                };

            opts.TotalRequestTimeout =
                new HttpTimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(
                        options.TimeoutSeconds *
                        (options.RetryCount + 1))
                };
        });

        return builder;
    }

    /// <summary>
    /// Resiliência padrão para APIs externas.
    /// </summary>
    public static IHttpClientBuilder AddDefaultResilience(
        this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(opts =>
        {
            opts.Retry = new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,

                Delay = TimeSpan.FromMilliseconds(200),

                BackoffType = DelayBackoffType.Exponential,

                UseJitter = true
            };

            opts.AttemptTimeout =
                new HttpTimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };

            opts.TotalRequestTimeout =
                new HttpTimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
        });

        return builder;
    }
}