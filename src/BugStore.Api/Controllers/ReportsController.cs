using BugStore.Api.Controllers.Models;
using BugStore.Application.Handlers.Reports;
using BugStore.Application.Responses.Reports;
using Microsoft.AspNetCore.Mvc;

namespace BugStore.Api.Controllers;

[ApiController]
[Route("v1/reports")]
public class ReportsController(ReportHandler handler) : ControllerBase
{
    [HttpGet("sales-by-customer/{customerId:guid}")]
    public async Task<IActionResult> GetSalesByCustomerAsync(Guid customerId, [FromQuery] ReportQuery query, CancellationToken cancellationToken)
    {
        if (query.StartDate.HasValue && query.EndDate.HasValue && query.StartDate > query.EndDate)
            return BadRequest("O parâmetro startDate não pode ser maior que endDate.");

        var result = await handler.GetSalesByCustomerAsync(customerId, query.StartDate, query.EndDate, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("revenue-by-period")]
    public async Task<IActionResult> GetRevenueByPeriodAsync([FromQuery] ReportQuery query, CancellationToken cancellationToken)
    {
        if (query.StartDate.HasValue && query.EndDate.HasValue && query.StartDate > query.EndDate)
            return BadRequest("O parâmetro startDate não pode ser maior que endDate.");

        var (isValid, period) = TryParsePeriod(query.GroupBy);
        if (!isValid)
            return BadRequest("Valor inválido para groupBy. Utilize day, month ou year.");

        var result = await handler.GetRevenueByPeriodAsync(query.StartDate, query.EndDate, period, cancellationToken);
        return Ok(result);
    }

    private static (bool IsValid, RevenuePeriod Period) TryParsePeriod(string? groupBy) =>
        (groupBy ?? "day").ToLowerInvariant() switch
        {
            "day" or "" => (true, RevenuePeriod.Day),
            "month" => (true, RevenuePeriod.Month),
            "year" => (true, RevenuePeriod.Year),
            _ => (false, RevenuePeriod.Day)
        };
}
