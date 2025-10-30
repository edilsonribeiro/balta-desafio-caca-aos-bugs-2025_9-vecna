using BugStore.Application.Responses.Reports;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace BugStore.Application.Handlers.Reports;

public record GetSalesByCustomerQuery(
    Guid CustomerId,
    DateTime? StartDate,
    DateTime? EndDate) : IRequest<SalesByCustomerResponse?>;

public record GetRevenueByPeriodQuery(
    DateTime? StartDate,
    DateTime? EndDate,
    RevenuePeriod Period = RevenuePeriod.Day) : IRequest<IReadOnlyList<RevenueByPeriodResponse>>;

public class ReportHandler(AppDbContext context) :
    IRequestHandler<GetSalesByCustomerQuery, SalesByCustomerResponse?>,
    IRequestHandler<GetRevenueByPeriodQuery, IReadOnlyList<RevenueByPeriodResponse>>
{
    public Task<SalesByCustomerResponse?> Handle(GetSalesByCustomerQuery request, CancellationToken cancellationToken) =>
        GetSalesByCustomerAsync(request.CustomerId, request.StartDate, request.EndDate, cancellationToken);

    public Task<IReadOnlyList<RevenueByPeriodResponse>> Handle(GetRevenueByPeriodQuery request, CancellationToken cancellationToken) =>
        GetRevenueByPeriodAsync(request.StartDate, request.EndDate, request.Period, cancellationToken);

    public async Task<SalesByCustomerResponse?> GetSalesByCustomerAsync(
        Guid customerId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var customer = await context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer is null)
            return null;

        var (normalizedStart, normalizedEnd) = NormalizeDateRange(startDate, endDate);

        var ordersQuery = context.Orders
            .AsNoTracking()
            .Include(order => order.Lines)
                .ThenInclude(line => line.Product)
            .Where(order => order.CustomerId == customerId);

        if (normalizedStart.HasValue)
            ordersQuery = ordersQuery.Where(order => order.CreatedAt >= normalizedStart.Value);

        if (normalizedEnd.HasValue)
            ordersQuery = ordersQuery.Where(order => order.CreatedAt <= normalizedEnd.Value);

        var orders = await ordersQuery
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

        var orderResponses = orders
            .Select(order =>
            {
                var lines = order.Lines
                    .Select(line =>
                    {
                        var total = RoundCurrency(line.Total);
                        var quantity = line.Quantity;
                        var product = line.Product;
                        var productTitle = product?.Title ?? string.Empty;
                        var productDescription = product?.Description ?? string.Empty;
                        var productPrice = RoundCurrency(product?.Price ?? 0m);

                        return new SalesByCustomerOrderLine(
                            line.Id,
                            line.ProductId,
                            productTitle,
                            productDescription,
                            productPrice,
                            quantity,
                            total);
                    })
                    .ToList();

                var totalAmount = RoundCurrency(lines.Sum(line => line.Total));

                return new SalesByCustomerOrder(
                    order.Id,
                    totalAmount,
                    NormalizeTimestamp(order.CreatedAt),
                    NormalizeTimestamp(order.UpdatedAt),
                    lines);
            })
            .ToList();

        var ordersCount = orderResponses.Count;
        var totalItems = orderResponses.Sum(order => order.Lines.Sum(line => line.Quantity));
        var totalAmount = RoundCurrency(orderResponses.Sum(order => order.Total));

        return new SalesByCustomerResponse(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            ordersCount,
            totalItems,
            totalAmount,
            orderResponses);
    }

    public async Task<IReadOnlyList<RevenueByPeriodResponse>> GetRevenueByPeriodAsync(
        DateTime? startDate,
        DateTime? endDate,
        RevenuePeriod period = RevenuePeriod.Day,
        CancellationToken cancellationToken = default)
    {
        var (normalizedStart, normalizedEnd) = NormalizeDateRange(startDate, endDate);

        var ordersQuery = context.Orders.AsNoTracking();

        if (normalizedStart.HasValue)
            ordersQuery = ordersQuery.Where(order => order.CreatedAt >= normalizedStart.Value);

        if (normalizedEnd.HasValue)
            ordersQuery = ordersQuery.Where(order => order.CreatedAt <= normalizedEnd.Value);

        var orderSummaries = await ordersQuery
            .Select(order => new OrderRevenueSummary(
                order.CreatedAt,
                order.Lines.Sum(line => line.Total),
                order.Lines.Sum(line => line.Quantity)))
            .ToListAsync(cancellationToken);

        var grouped = orderSummaries
            .GroupBy(summary => GetPeriodStart(summary.CreatedAt, period))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var periodStart = group.Key;
                var periodEnd = GetPeriodEnd(periodStart, period);
                var ordersCount = group.Count();
                var rawTotalAmount = group.Sum(item => item.Total);
                var totalAmount = RoundCurrency(rawTotalAmount);
                var totalItems = group.Sum(item => item.TotalItems);
                var averageTicket = RoundCurrency(ordersCount > 0 ? rawTotalAmount / ordersCount : 0m);
                var largestOrder = RoundCurrency(group.Max(item => item.Total));
                var smallestOrder = RoundCurrency(group.Min(item => item.Total));
                return new RevenueByPeriodResponse(
                    NormalizeTimestamp(periodStart),
                    NormalizeTimestamp(periodEnd),
                    totalAmount,
                    ordersCount,
                    totalItems,
                    averageTicket,
                    largestOrder,
                    smallestOrder);
            })
            .ToList();

        return grouped;
    }

    private static (DateTime? Start, DateTime? End) NormalizeDateRange(DateTime? start, DateTime? end)
    {
        if (start.HasValue && start.Value.Kind == DateTimeKind.Unspecified)
            start = DateTime.SpecifyKind(start.Value, DateTimeKind.Utc);

        if (end.HasValue && end.Value.Kind == DateTimeKind.Unspecified)
            end = DateTime.SpecifyKind(end.Value, DateTimeKind.Utc);

        if (start.HasValue && end.HasValue && start > end)
            return (end, start);

        return (start, end);
    }

    private static DateTime GetPeriodStart(DateTime timestamp, RevenuePeriod period)
    {
        var utcDateTime = timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
            _ => timestamp.ToUniversalTime()
        };

        return period switch
        {
            RevenuePeriod.Year => new DateTime(utcDateTime.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            RevenuePeriod.Month => new DateTime(utcDateTime.Year, utcDateTime.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => new DateTime(utcDateTime.Year, utcDateTime.Month, utcDateTime.Day, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static DateTime GetPeriodEnd(DateTime periodStart, RevenuePeriod period)
    {
        return period switch
        {
            RevenuePeriod.Year => periodStart.AddYears(1).AddTicks(-1),
            RevenuePeriod.Month => periodStart.AddMonths(1).AddTicks(-1),
            _ => periodStart.AddDays(1).AddTicks(-1)
        };
    }

    private sealed record OrderRevenueSummary(DateTime CreatedAt, decimal Total, int TotalItems);

    private static decimal RoundCurrency(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static DateTime NormalizeTimestamp(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };

        var trimmedTicks = utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond);
        return new DateTime(trimmedTicks, DateTimeKind.Utc);
    }
}
