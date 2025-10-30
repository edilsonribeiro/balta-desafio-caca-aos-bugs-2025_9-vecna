using BugStore.Api.Controllers.Models;
using BugStore.Application.Handlers.Products;
using BugStore.Application.Requests.Products;
using Microsoft.AspNetCore.Mvc;

namespace BugStore.Api.Controllers;

[ApiController]
[Route("v1/products")]
public class ProductsController(ProductHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var products = await handler.SearchAsync(
            query.Term,
            query.Page,
            query.PageSize,
            query.SortBy,
            query.SortOrder,
            cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id:guid}", Name = nameof(GetProductByIdAsync))]
    public async Task<IActionResult> GetProductByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await handler.GetByIdAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] Create request, CancellationToken cancellationToken)
    {
        var product = await handler.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProductByIdAsync), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] Update request, CancellationToken cancellationToken)
    {
        var product = await handler.UpdateAsync(id, request, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await handler.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
