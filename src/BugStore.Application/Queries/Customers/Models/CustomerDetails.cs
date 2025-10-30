namespace BugStore.Application.Queries.Customers.Models;

public record CustomerDetails(Guid Id, string Name, string Email, string Phone, DateTime BirthDate);
