namespace BugStore.Application.Responses.Customers;

public record Create(Guid Id, string Name, string Email, string Phone, DateTime BirthDate);
