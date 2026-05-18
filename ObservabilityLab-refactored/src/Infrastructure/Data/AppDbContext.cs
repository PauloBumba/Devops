using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Application.Abstractions;
using ObservabilityLab.Domain.Entities;
using ObservabilityLab.Infrastructure.Data.Configurations;

namespace ObservabilityLab.Infrastructure.Data;

/// <summary>
/// Implementação do DbContext. Implementa IAppDbContext para inversão de dependência.
/// Application nunca referencia esta classe diretamente.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica todas as IEntityTypeConfiguration<T> encontradas nesta assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
