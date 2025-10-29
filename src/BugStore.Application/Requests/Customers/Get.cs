namespace BugStore.Application.Requests.Customers;

public record Get(int Page = 1, int PageSize = 25);