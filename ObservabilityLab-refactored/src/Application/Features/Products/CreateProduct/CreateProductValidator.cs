using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Application.Common;

namespace ObservabilityLab.Application.Features.Products.CreateProduct;

/// <summary>
/// Validação do comando CreateProduct.
/// Único arquivo — única responsabilidade: validar a entrada antes de executar o handler.
/// </summary>
public sealed class CreateProductValidator : IValidator<CreateProductCommand>
{
    private const int    MaxNameLength = 200;
    private const decimal MaxPrice     = 1_000_000m;

    public ValidationResult Validate(CreateProductCommand cmd)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(cmd.Name))
            result.AddError("Name is required.");

        if (cmd.Name?.Length > MaxNameLength)
            result.AddError($"Name must be at most {MaxNameLength} characters.");

        if (cmd.Price <= 0)
            result.AddError("Price must be a positive value.");

        if (cmd.Price > MaxPrice)
            result.AddError($"Price cannot exceed {MaxPrice:N0}.");

        if (string.IsNullOrWhiteSpace(cmd.Category))
            result.AddError("Category is required.");

        return result;
    }
}
