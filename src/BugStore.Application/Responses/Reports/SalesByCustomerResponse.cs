namespace BugStore.Application.Responses.Reports;

public sealed record SalesByCustomerResponse(
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    int OrdersCount,
    int TotalItems,
    decimal TotalAmount,
    IReadOnlyList<SalesByCustomerOrder> Orders);

public sealed record SalesByCustomerOrder(
    Guid Id,
    decimal Total,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<SalesByCustomerOrderLine> Lines);

public sealed record SalesByCustomerOrderLine(
    Guid Id,
    Guid ProductId,
    string ProductTitle,
    string ProductDescription,
    decimal ProductPrice,
    int Quantity,
    decimal Total);
