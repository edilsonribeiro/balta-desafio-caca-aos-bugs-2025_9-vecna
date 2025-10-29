namespace BugStore.Application.Responses.Products;

public record Create(Guid Id, string Title, string Description, string Slug, decimal Price);
