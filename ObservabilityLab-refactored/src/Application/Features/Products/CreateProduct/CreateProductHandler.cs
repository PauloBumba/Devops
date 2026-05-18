using MediatR;
using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Domain.Entities;

namespace ObservabilityLab.Application.Features.Products.CreateProduct;

/// <summary>
/// Handler do comando CreateProduct.
/// Depende de IAppDbContext (abstração) — não conhece EF Core diretamente.
/// </summary>
public sealed class CreateProductHandler(IAppDbContext db) : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var product = Product.Create(request.Name, request.Price, request.Category);

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        return new ProductDto(product.Id, product.Name, product.Price, product.Category, product.CreatedAt);
    }
}
