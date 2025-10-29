namespace BugStore.Application.Responses.Products;

public record GetById(Guid Id, string Title, string Description, string Slug, decimal Price);
