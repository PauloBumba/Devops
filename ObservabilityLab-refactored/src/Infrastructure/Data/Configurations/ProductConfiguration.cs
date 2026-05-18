using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObservabilityLab.Domain.Entities;

namespace ObservabilityLab.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core da entidade Product via Fluent API.
/// Separado em arquivo próprio — responsabilidade única: mapeamento ORM.
/// </summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .UseIdentityByDefaultColumn();

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.HasIndex(p => p.Category)
            .HasDatabaseName("ix_products_category");

        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("ix_products_created_at");
    }
}
