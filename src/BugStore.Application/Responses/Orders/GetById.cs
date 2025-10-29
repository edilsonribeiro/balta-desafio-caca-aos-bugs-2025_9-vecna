namespace BugStore.Application.Responses.Orders;

public record GetById(
    Guid Id,
    Guid CustomerId,
    decimal Total,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<Line> Lines);
