using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Application.Features.Products.CreateProduct;

namespace ObservabilityLab.Application.Features.Products.GetProductCached;

/// <summary>Query com cache — busca no Redis antes de ir ao banco.</summary>
public sealed record GetProductCachedQuery(int Id) : IRequest<ProductDto?>;

/// <summary>
/// Handler cache-aside: L1 Redis → L2 DB.
/// IDistributedCache é uma abstração do .NET, não depende de implementação.
/// </summary>
public sealed class GetProductCachedHandler(IAppDbContext db, IDistributedCache cache)
    : IRequestHandler<GetProductCachedQuery, ProductDto?>
{
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public async Task<ProductDto?> Handle(GetProductCachedQuery request, CancellationToken ct)
    {
        var cacheKey = $"product:{request.Id}";
        var cached   = await cache.GetStringAsync(cacheKey, ct);

        if (cached is not null)
            return JsonSerializer.Deserialize<ProductDto>(cached);

        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Category, p.CreatedAt))
            .FirstOrDefaultAsync(ct);

        if (product is null) return null;

        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(product), CacheOptions, ct);
        return product;
    }
}
