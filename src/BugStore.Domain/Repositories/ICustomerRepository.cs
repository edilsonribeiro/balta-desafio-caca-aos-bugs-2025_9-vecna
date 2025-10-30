using BugStore.Domain.Entities;

namespace BugStore.Domain.Repositories;

public interface ICustomerRepository
{
    IQueryable<Customer> Query();

    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);

    void Remove(Customer customer);
}
