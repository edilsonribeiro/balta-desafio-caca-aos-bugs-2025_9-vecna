namespace BugStore.Application.Responses.Customers;

public record Update(Guid Id, string Name, string Email, string Phone, DateTime BirthDate);
