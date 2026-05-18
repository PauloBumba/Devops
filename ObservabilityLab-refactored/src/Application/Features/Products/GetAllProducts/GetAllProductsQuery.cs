using MediatR;
using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Application.Features.Products.CreateProduct;

namespace ObservabilityLab.Application.Features.Products.GetAllProducts;

/// <summary>Query sem parâmetros — lista todos os produtos.</summary>
public sealed record GetAllProductsQuery : IRequest<IReadOnlyList<ProductDto>>;

/// <summary>
/// Handler: busca direto no banco, AsNoTracking para performance de leitura.
/// </summary>
public sealed class GetAllProductsHandler(IAppDbContext db)
    : IRequestHandler<GetAllProductsQuery, IReadOnlyList<ProductDto>>
{
    public async Task<IReadOnlyList<ProductDto>> Handle(GetAllProductsQuery request, CancellationToken ct)
        => await db.Products
            .AsNoTracking()
            .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Category, p.CreatedAt))
            .ToListAsync(ct);
}
