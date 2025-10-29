namespace BugStore.Application.Responses.Orders;

public record Search(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    decimal Total,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<Line> Lines);
