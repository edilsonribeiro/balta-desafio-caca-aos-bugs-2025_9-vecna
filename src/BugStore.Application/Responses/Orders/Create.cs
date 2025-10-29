namespace BugStore.Application.Responses.Orders;

public record Create(
    Guid Id,
    Guid CustomerId,
    decimal Total,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<Line> Lines);
