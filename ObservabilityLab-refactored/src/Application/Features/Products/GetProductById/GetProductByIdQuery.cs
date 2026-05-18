using MediatR;
using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Application.Features.Products.CreateProduct;

namespace ObservabilityLab.Application.Features.Products.GetProductById;

/// <summary>Query por ID — retorna null se não encontrado (sem lançar exceção).</summary>
public sealed record GetProductByIdQuery(int Id) : IRequest<ProductDto?>;

/// <summary>Handler: projeção direta para DTO sem carregar a entidade completa.</summary>
public sealed class GetProductByIdHandler(IAppDbContext db)
    : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken ct)
        => await db.Products
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Category, p.CreatedAt))
            .FirstOrDefaultAsync(ct);
}
