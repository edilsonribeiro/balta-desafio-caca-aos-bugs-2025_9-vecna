namespace BugStore.Api.Controllers.Models;

public record ListQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 25,
    string? SortBy = null,
    string? SortOrder = null);
