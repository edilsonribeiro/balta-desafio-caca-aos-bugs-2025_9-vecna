namespace BugStore.Application.Responses.Reports;

public sealed record SalesByCustomerResponse(
    Guid CustomerId,
    string CustomerName,
    int OrdersCount,
    int TotalItems,
    decimal TotalAmount,
    decimal AverageTicket,
    decimal LargestOrderTotal,
    decimal SmallestOrderTotal,
    DateTime FirstOrderAt,
    DateTime LastOrderAt);
