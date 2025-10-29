namespace BugStore.Application.Responses.Reports;

public enum RevenuePeriod
{
    Day,
    Month,
    Year
}

public sealed record RevenueByPeriodResponse(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal TotalAmount,
    int OrdersCount,
    int TotalItems,
    decimal AverageTicket,
    decimal LargestOrderTotal,
    decimal SmallestOrderTotal);
