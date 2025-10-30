using BugStore.Application.Queries.Customers.Models;
using BugStore.Application.Responses.Common;
using MediatR;

namespace BugStore.Application.Handlers.Customers.Queries;

public record SearchCustomersQuery(
    string? Term,
    int Page = 1,
    int PageSize = 25,
    string? SortBy = null,
    string? SortOrder = null) : IRequest<PagedResult<CustomerListItem>>;
