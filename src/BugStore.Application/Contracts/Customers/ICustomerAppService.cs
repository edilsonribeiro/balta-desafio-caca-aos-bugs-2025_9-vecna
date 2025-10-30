using BugStore.Application.Responses.Common;
using CreateCustomerRequest = BugStore.Application.Requests.Customers.Create;
using CreateCustomerResponse = BugStore.Application.Responses.Customers.Create;
using GetCustomerByIdResponse = BugStore.Application.Responses.Customers.GetById;
using GetCustomersResponse = BugStore.Application.Responses.Customers.Get;
using UpdateCustomerRequest = BugStore.Application.Requests.Customers.Update;
using UpdateCustomerResponse = BugStore.Application.Responses.Customers.Update;

namespace BugStore.Application.Contracts.Customers;

public interface ICustomerAppService
{
    Task<PagedResult<GetCustomersResponse>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        CancellationToken cancellationToken = default);

    Task<GetCustomerByIdResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CreateCustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<UpdateCustomerResponse?> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
