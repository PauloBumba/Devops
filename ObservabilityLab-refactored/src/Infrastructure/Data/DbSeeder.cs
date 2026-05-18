using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Domain.Entities;

namespace ObservabilityLab.Infrastructure.Data;

/// <summary>
/// Responsabilidade única: seed de dados iniciais no banco.
/// Chamado em Program.cs apenas se a tabela estiver vazia.
/// </summary>
public static class DbSeeder
{
    private static readonly string[] Categories =
        ["Electronics", "Books", "Clothing", "Food", "Sports", "Home", "Toys"];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Products.AnyAsync(ct)) return;

        var products = Enumerable.Range(1, 100).Select(i =>
            Product.Create(
                name:     $"Product {i:D3} — {Categories[i % Categories.Length]}",
                price:    Math.Round((decimal)(Random.Shared.NextDouble() * 1000 + 1), 2),
                category: Categories[i % Categories.Length]));

        db.Products.AddRange(products);
        await db.SaveChangesAsync(ct);
    }
}
