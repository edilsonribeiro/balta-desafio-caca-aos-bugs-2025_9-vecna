using MediatR;

namespace BugStore.Application.Handlers.Customers.Commands;

public record DeleteCustomerCommand(Guid Id) : IRequest<bool>;
