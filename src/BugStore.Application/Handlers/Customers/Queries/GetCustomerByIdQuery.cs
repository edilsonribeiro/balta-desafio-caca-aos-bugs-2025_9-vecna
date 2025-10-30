using BugStore.Application.Queries.Customers.Models;
using MediatR;

namespace BugStore.Application.Handlers.Customers.Queries;

public record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerDetails?>;
