using MediatR;
using UpdateCustomerRequest = BugStore.Application.Requests.Customers.Update;
using UpdateCustomerResponse = BugStore.Application.Responses.Customers.Update;

namespace BugStore.Application.Handlers.Customers.Commands;

public record UpdateCustomerCommand(Guid Id, UpdateCustomerRequest Request) : IRequest<UpdateCustomerResponse?>;
