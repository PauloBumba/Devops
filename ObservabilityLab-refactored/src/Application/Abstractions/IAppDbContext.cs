using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Domain;
using ObservabilityLab.Domain.Entities;

namespace ObservabilityLab.Application.Abstractions;

/// <summary>
/// Contrato do DbContext exposto para a camada Application.
/// Inverte a dependência: Application não conhece EF Core nem Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<Product> Products { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
