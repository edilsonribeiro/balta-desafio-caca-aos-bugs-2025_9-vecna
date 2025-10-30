using BugStore.Api.Controllers.Models;
using BugStore.Application.Handlers.Customers;
using BugStore.Application.Requests.Customers;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace BugStore.Api.Controllers;

[ApiController]
[Route("v1/customers")]
public class CustomersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var customers = await mediator.Send(
            new SearchCustomersQuery(
                query.Term,
                query.Page,
                query.PageSize,
                query.SortBy,
                query.SortOrder),
            cancellationToken);

        return Ok(customers);
    }

    [HttpGet("{id:guid}", Name = nameof(GetCustomerByIdAsync))]
    public async Task<IActionResult> GetCustomerByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await mediator.Send(new GetCustomerByIdQuery(id), cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] Create request, CancellationToken cancellationToken)
    {
        var customer = await mediator.Send(new CreateCustomerCommand(request), cancellationToken);
        return CreatedAtRoute(nameof(GetCustomerByIdAsync), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] Update request, CancellationToken cancellationToken)
    {
        var customer = await mediator.Send(new UpdateCustomerCommand(id, request), cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await mediator.Send(new DeleteCustomerCommand(id), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
