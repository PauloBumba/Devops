using ObservabilityLab.Application.Common;

namespace ObservabilityLab.Application.Abstractions;

/// <summary>
/// Contrato de validação desacoplado de FluentValidation.
/// Permite trocar a lib de validação sem alterar os Behaviors.
/// </summary>
public interface IValidator<T>
{
    ValidationResult Validate(T instance);
}
