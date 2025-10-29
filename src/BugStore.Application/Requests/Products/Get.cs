namespace BugStore.Application.Requests.Products;

public record Get(int Page = 1, int PageSize = 25);
