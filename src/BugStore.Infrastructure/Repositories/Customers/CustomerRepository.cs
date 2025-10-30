using BugStore.Domain.Entities;
using BugStore.Domain.Repositories;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BugStore.Infrastructure.Repositories.Customers;

public class CustomerRepository(AppDbContext context) : ICustomerRepository
{
    public IQueryable<Customer> Query() =>
        context.Customers.AsQueryable();

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Customers.FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public Task AddAsync(Customer customer, CancellationToken cancellationToken = default) =>
        context.Customers.AddAsync(customer, cancellationToken).AsTask();

    public void Remove(Customer customer) =>
        context.Customers.Remove(customer);
}
