namespace BugStore.Application.Responses.Customers;

public record Get(Guid Id, string Name, string Email, string Phone, DateTime BirthDate);
