namespace BugStore.Application.Responses.Orders;

public record Line(Guid Id, Guid ProductId, string ProductTitle, int Quantity, decimal Total);
