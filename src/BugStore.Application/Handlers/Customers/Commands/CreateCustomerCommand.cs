using MediatR;
using CreateCustomerRequest = BugStore.Application.Requests.Customers.Create;
using CreateCustomerResponse = BugStore.Application.Responses.Customers.Create;

namespace BugStore.Application.Handlers.Customers.Commands;

public record CreateCustomerCommand(CreateCustomerRequest Request) : IRequest<CreateCustomerResponse>;
