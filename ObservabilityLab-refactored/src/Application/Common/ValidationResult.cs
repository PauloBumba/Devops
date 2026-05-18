namespace ObservabilityLab.Application.Common;

/// <summary>
/// Resultado de validação com lista de erros acumulados.
/// Utilizado pelo ValidationBehavior do pipeline MediatR.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<string> _errors = [];

    public bool          IsValid => _errors.Count == 0;
    public IList<string> Errors  => _errors.AsReadOnly();

    public void AddError(string error) => _errors.Add(error);

    public static ValidationResult Success() => new();

    public static ValidationResult Failure(params string[] errors)
    {
        var result = new ValidationResult();
        foreach (var e in errors) result.AddError(e);
        return result;
    }
}
