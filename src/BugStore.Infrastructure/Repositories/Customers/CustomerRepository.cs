using BugStore.Domain.Entities;
using BugStore.Domain.Repositories;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BugStore.Infrastructure.Repositories.Customers;

public class CustomerRepository(AppDbContext context) : ICustomerRepository
{
    private static readonly Func<AppDbContext, Guid, CancellationToken, Task<Customer?>> GetByIdCompiled =
        EF.CompileAsyncQuery((AppDbContext dbContext, Guid id, CancellationToken cancellationToken) =>
            dbContext.Customers.FirstOrDefault(customer => customer.Id == id));

    public IQueryable<Customer> Query() =>
        context.Customers.AsQueryable();

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdCompiled(context, id, cancellationToken);

    public Task AddAsync(Customer customer, CancellationToken cancellationToken = default) =>
        context.Customers.AddAsync(customer, cancellationToken).AsTask();

    public void Remove(Customer customer) =>
        context.Customers.Remove(customer);
}
