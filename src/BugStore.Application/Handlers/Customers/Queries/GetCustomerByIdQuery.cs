using BugStore.Application.Responses.Customers;
using MediatR;

namespace BugStore.Application.Handlers.Customers.Queries;

public record GetCustomerByIdQuery(Guid Id) : IRequest<GetById?>;
