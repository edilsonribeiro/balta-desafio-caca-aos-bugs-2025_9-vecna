using BugStore.Application.Contracts.Customers;
using BugStore.Application.Handlers.Customers;
using BugStore.Application.Responses.Common;
using MediatR;
using CreateCustomerRequest = BugStore.Application.Requests.Customers.Create;
using CreateCustomerResponse = BugStore.Application.Responses.Customers.Create;
using GetCustomerByIdResponse = BugStore.Application.Responses.Customers.GetById;
using GetCustomersResponse = BugStore.Application.Responses.Customers.Get;
using UpdateCustomerRequest = BugStore.Application.Requests.Customers.Update;
using UpdateCustomerResponse = BugStore.Application.Responses.Customers.Update;

namespace BugStore.Application.Services.Customers;

public class CustomerAppService(IMediator mediator) : ICustomerAppService
{
    public Task<PagedResult<GetCustomersResponse>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        CancellationToken cancellationToken = default)
    {
        var query = new SearchCustomersQuery(term, page, pageSize, sortBy, sortOrder);
        return mediator.Send(query, cancellationToken);
    }

    public Task<GetCustomerByIdResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = new GetCustomerByIdQuery(id);
        return mediator.Send(query, cancellationToken);
    }

    public Task<CreateCustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var command = new CreateCustomerCommand(request);
        return mediator.Send(command, cancellationToken);
    }

    public Task<UpdateCustomerResponse?> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var command = new UpdateCustomerCommand(id, request);
        return mediator.Send(command, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var command = new DeleteCustomerCommand(id);
        return mediator.Send(command, cancellationToken);
    }
}
