namespace BugStore.Application.Requests.Orders;

public class Create
{
    public Guid CustomerId { get; init; }
    public List<Line> Lines { get; init; } = [];

    public record Line(Guid ProductId, int Quantity);
}
