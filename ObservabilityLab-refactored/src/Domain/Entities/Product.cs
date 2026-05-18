namespace ObservabilityLab.Domain.Entities;

/// <summary>
/// Aggregate root — representa um produto no catálogo.
/// Encapsula toda a lógica de negócio e invariantes de domínio.
/// </summary>
public sealed class Product
{
    public int            Id        { get; private set; }
    public string         Name      { get; private set; } = string.Empty;
    public decimal        Price     { get; private set; }
    public string         Category  { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core constructor (private — impede criação sem factory)
    private Product() { }

    /// <summary>Factory method garante invariantes antes de criar a entidade.</summary>
    public static Product Create(string name, decimal price, string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be positive.");

        return new Product
        {
            Name      = name.Trim(),
            Price     = Math.Round(price, 2),
            Category  = category.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(newPrice), "Price must be positive.");

        Price = Math.Round(newPrice, 2);
    }
}
