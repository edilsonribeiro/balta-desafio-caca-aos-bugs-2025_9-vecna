using BugStore.Api.Controllers.Models;
using BugStore.Application.Handlers.Customers;
using BugStore.Application.Requests.Customers;
using Microsoft.AspNetCore.Mvc;

namespace BugStore.Api.Controllers;

[ApiController]
[Route("v1/customers")]
public class CustomersController(CustomerHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var customers = await handler.SearchAsync(
            query.Term,
            query.Page,
            query.PageSize,
            query.SortBy,
            query.SortOrder,
            cancellationToken);

        return Ok(customers);
    }

    [HttpGet("{id:guid}", Name = nameof(GetCustomerByIdAsync))]
    public async Task<IActionResult> GetCustomerByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await handler.GetByIdAsync(id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] Create request, CancellationToken cancellationToken)
    {
        var customer = await handler.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCustomerByIdAsync), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] Update request, CancellationToken cancellationToken)
    {
        var customer = await handler.UpdateAsync(id, request, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await handler.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
