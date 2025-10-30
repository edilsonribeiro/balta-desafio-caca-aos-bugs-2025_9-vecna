using BugStore.Api.Controllers.Models;
using BugStore.Application.Handlers.Orders;
using BugStore.Application.Requests.Orders;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace BugStore.Api.Controllers;

[ApiController]
[Route("v1/orders")]
public class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var orders = await mediator.Send(
            new SearchOrdersQuery(
                query.Term,
                query.Page,
                query.PageSize,
                query.SortBy,
                query.SortOrder),
            cancellationToken);

        return Ok(orders);
    }

    [HttpGet("{id:guid}", Name = nameof(GetOrderByIdAsync))]
    public async Task<IActionResult> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await mediator.Send(new GetOrderByIdQuery(id), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] Create request, CancellationToken cancellationToken)
    {
        var order = await mediator.Send(new CreateOrderCommand(request), cancellationToken);
        if (order is null)
            return BadRequest();

        return CreatedAtRoute(nameof(GetOrderByIdAsync), new { id = order.Id }, order);
    }
}
