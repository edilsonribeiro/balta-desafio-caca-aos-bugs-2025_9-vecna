using BugStore.Application.Responses.Reports;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BugStore.Application.Handlers.Reports;

public class ReportHandler(AppDbContext context)
{
    public async Task<IReadOnlyList<SalesByCustomerResponse>> GetSalesByCustomerAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var (normalizedStart, normalizedEnd) = NormalizeDateRange(startDate, endDate);

        var query = context.Orders.AsNoTracking();

        if (normalizedStart.HasValue)
            query = query.Where(order => order.CreatedAt >= normalizedStart.Value);

        if (normalizedEnd.HasValue)
            query = query.Where(order => order.CreatedAt <= normalizedEnd.Value);

        var orderSummaries = await query
            .Select(order => new OrderCustomerSummary(
                order.CustomerId,
                order.Customer != null ? order.Customer.Name : string.Empty,
                order.CreatedAt,
                order.Lines.Sum(line => line.Quantity),
                order.Lines.Sum(line => line.Total)))
            .ToListAsync(cancellationToken);

        return orderSummaries
            .GroupBy(summary => new { summary.CustomerId, summary.CustomerName })
            .Select(group =>
            {
                var ordersCount = group.Count();
                var totalItems = group.Sum(item => item.TotalItems);
                var rawTotalAmount = group.Sum(item => item.TotalAmount);
                var totalAmount = RoundCurrency(rawTotalAmount);
                var averageTicket = RoundCurrency(ordersCount > 0 ? rawTotalAmount / ordersCount : 0m);
                var largestOrder = RoundCurrency(group.Max(item => item.TotalAmount));
                var smallestOrder = RoundCurrency(group.Min(item => item.TotalAmount));
                var firstOrderAt = NormalizeTimestamp(group.Min(item => item.CreatedAt));
                var lastOrderAt = NormalizeTimestamp(group.Max(item => item.CreatedAt));

                return new SalesByCustomerResponse(
                    group.Key.CustomerId,
                    group.Key.CustomerName,
                    ordersCount,
                    totalItems,
                    totalAmount,
                    averageTicket,
                    largestOrder,
                    smallestOrder,
                    firstOrderAt,
                    lastOrderAt);
            })
            .OrderByDescending(result => result.TotalAmount)
            .ThenBy(result => result.CustomerName)
            .ToList();
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
    private sealed record OrderCustomerSummary(Guid CustomerId, string CustomerName, DateTime CreatedAt, int TotalItems, decimal TotalAmount);

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
