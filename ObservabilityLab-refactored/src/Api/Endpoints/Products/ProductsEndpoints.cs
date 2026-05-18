using MediatR;
using ObservabilityLab.Application.Features.Products.CreateProduct;
using ObservabilityLab.Application.Features.Products.GetAllProducts;
using ObservabilityLab.Application.Features.Products.GetProductById;
using ObservabilityLab.Application.Features.Products.GetProductCached;

namespace ObservabilityLab.Api.Endpoints.Products;

/// <summary>
/// Endpoints de Produtos agrupados por Route Group.
/// Cada handler delega ao MediatR — zero lógica de negócio aqui.
/// </summary>
public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products")
            .WithTags("Products")
            .WithOpenApi();

        group.MapGet("/", GetAll)
            .WithName("GetAllProducts")
            .WithSummary("Lista todos os produtos (direto do banco)");

        group.MapGet("/{id:int}", GetById)
            .WithName("GetProductById")
            .WithSummary("Busca produto por ID");

        group.MapGet("/cached/{id:int}", GetCached)
            .WithName("GetProductCached")
            .WithSummary("Busca produto com cache Redis (L1+L2)");

        group.MapPost("/", Create)
            .WithName("CreateProduct")
            .WithSummary("Cria um novo produto");

        return app;
    }

    private static async Task<IResult> GetAll(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAllProductsQuery(), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetById(int id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetProductByIdQuery(id), ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> GetCached(int id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetProductCachedQuery(id), ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> Create(
        CreateProductRequest req,
        IMediator            mediator,
        CancellationToken    ct)
    {
        var command = new CreateProductCommand(req.Name, req.Price, req.Category);
        var result  = await mediator.Send(command, ct);
        return Results.Created($"/api/v1/products/{result.Id}", result);
    }
}

/// <summary>Request body para criação de produto.</summary>
public sealed record CreateProductRequest(string Name, decimal Price, string Category);
