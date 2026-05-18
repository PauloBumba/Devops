using MediatR;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Application.Abstractions;

namespace ObservabilityLab.Application.Behaviors;


public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 ct)
    {
        var validatorList = validators.ToList();
        if (validatorList.Count == 0) return await next();

        var failures = validatorList
            .Select(v => v.Validate(request))
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Count == 0) return await next();

        var requestName = typeof(TRequest).Name;
        logger.LogWarning("Validation failed for {RequestType}: {Errors}", requestName, failures);

        throw new ArgumentException($"Validation failed for {requestName}: {string.Join("; ", failures)}");
    }
}
