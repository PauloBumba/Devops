using MediatR;
using ObservabilityLab.Domain.Entities;

namespace ObservabilityLab.Application.Features.Products.CreateProduct;

/// <summary>
/// Command para criar um novo produto.
/// Segue o padrão Vertical Slice: tudo relacionado a CreateProduct fica nesta pasta.
/// </summary>
public sealed record CreateProductCommand(
    string  Name,
    decimal Price,
    string  Category) : IRequest<ProductDto>;

/// <summary>DTO de resposta — imutável por design.</summary>
public sealed record ProductDto(
    int            Id,
    string         Name,
    decimal        Price,
    string         Category,
    DateTimeOffset CreatedAt);
