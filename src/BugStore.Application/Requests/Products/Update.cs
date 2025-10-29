namespace BugStore.Application.Requests.Products;

public class Update
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Slug { get; init; }
    public decimal Price { get; init; }
}
