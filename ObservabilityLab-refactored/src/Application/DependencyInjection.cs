using Microsoft.Extensions.DependencyInjection;
using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Application.Behaviors;
using ObservabilityLab.Application.Features.Auth.Login;
using ObservabilityLab.Application.Features.Products.CreateProduct;

namespace ObservabilityLab.Application;


public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // ── MediatR: registra todos os Handlers desta assembly ────────────
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);

            // Ordem de execução do pipeline (externo → interno):
            // ValidationBehavior → ObservabilityBehavior → Handler
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(ObservabilityBehavior<,>));
        });

        // ── Validators: registra automaticamente via reflection ───────────
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }

    /// <summary>
    /// Descobre e registra todos os IValidator&lt;T&gt; implementados nesta assembly.
    /// </summary>
    private static IServiceCollection AddValidatorsFromAssembly(
        this IServiceCollection services,
        System.Reflection.Assembly assembly)
    {
        var validatorType = typeof(IValidator<>);

        var registrations = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorType)
                .Select(i => new { Interface = i, Implementation = t }));

        foreach (var reg in registrations)
            services.AddScoped(reg.Interface, reg.Implementation);

        return services;
    }
}
