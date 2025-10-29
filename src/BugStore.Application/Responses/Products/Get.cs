namespace BugStore.Application.Responses.Products;

public record Get(Guid Id, string Title, string Description, string Slug, decimal Price);
