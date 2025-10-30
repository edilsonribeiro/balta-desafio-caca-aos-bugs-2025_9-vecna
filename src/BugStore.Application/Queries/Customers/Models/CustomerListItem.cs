namespace BugStore.Application.Queries.Customers.Models;

public record CustomerListItem(Guid Id, string Name, string Email, string Phone, DateTime BirthDate);
