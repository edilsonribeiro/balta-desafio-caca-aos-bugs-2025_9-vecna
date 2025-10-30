using BugStore.Application.Queries.Customers.Models;
using BugStore.Application.Responses.Common;
using CreateCustomerRequest = BugStore.Application.Requests.Customers.Create;
using CreateCustomerResponse = BugStore.Application.Responses.Customers.Create;
using UpdateCustomerRequest = BugStore.Application.Requests.Customers.Update;
using UpdateCustomerResponse = BugStore.Application.Responses.Customers.Update;

namespace BugStore.Application.Contracts.Customers;

public interface ICustomerAppService
{
    Task<PagedResult<CustomerListItem>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        CancellationToken cancellationToken = default);

    Task<CustomerDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CreateCustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<UpdateCustomerResponse?> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
