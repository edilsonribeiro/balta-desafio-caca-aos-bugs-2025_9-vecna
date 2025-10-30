using BugStore.Api.Controllers.Models;
using BugStore.Application.Handlers.Products;
using BugStore.Application.Requests.Products;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace BugStore.Api.Controllers;

[ApiController]
[Route("v1/products")]
public class ProductsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var products = await mediator.Send(
            new SearchProductsQuery(
                query.Term,
                query.Page,
                query.PageSize,
                query.SortBy,
                query.SortOrder),
            cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id:guid}", Name = nameof(GetProductByIdAsync))]
    public async Task<IActionResult> GetProductByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await mediator.Send(new GetProductByIdQuery(id), cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] Create request, CancellationToken cancellationToken)
    {
        var product = await mediator.Send(new CreateProductCommand(request), cancellationToken);
        return CreatedAtRoute(nameof(GetProductByIdAsync), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] Update request, CancellationToken cancellationToken)
    {
        var product = await mediator.Send(new UpdateProductCommand(id, request), cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await mediator.Send(new DeleteProductCommand(id), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
