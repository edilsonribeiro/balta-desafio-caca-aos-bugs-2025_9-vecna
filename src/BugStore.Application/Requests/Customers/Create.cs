namespace BugStore.Application.Requests.Customers;

public class Create
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public DateTime BirthDate { get; init; }
}