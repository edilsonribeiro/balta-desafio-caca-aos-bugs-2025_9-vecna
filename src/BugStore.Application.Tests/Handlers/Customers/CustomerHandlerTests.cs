using System.Linq;
using BugStore.Application.Handlers.Customers;
using BugStore.Application.Requests.Customers;
using BugStore.Application.Tests.Support;
using BugStore.Domain.Entities;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BugStore.Application.Tests.Handlers.Customers;

public sealed class CustomerHandlerTests
{
    [Fact]
    public async Task GetAsync_ShouldReturnCustomersOrderedByName()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedCustomersAsync(context, cancellationToken);
        var handler = new CustomerHandler(context);

        var customers = await handler.GetAsync(cancellationToken);

        Assert.Equal(3, customers.Count);
        Assert.Collection(customers,
            customer => Assert.Equal("Alfred Pennyworth", customer.Name),
            customer => Assert.Equal("Barbara Gordon", customer.Name),
            customer => Assert.Equal("Harvey Bullock", customer.Name));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnMatchingCustomer()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var customerId = Guid.Parse("93c3ca0d-1bd2-4a86-81c2-ba04c2bb0aac");
        context.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Lucius Fox",
            Email = "lucius@wayneenterprises.com",
            Phone = "+55 11 90000-0000",
            BirthDate = new DateTime(1960, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new CustomerHandler(context);

        var customer = await handler.GetByIdAsync(customerId, cancellationToken);

        Assert.NotNull(customer);
        Assert.Equal(customerId, customer.Id);
        Assert.Equal("Lucius Fox", customer.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenCustomerDoesNotExist()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new CustomerHandler(context);

        var customer = await handler.GetByIdAsync(Guid.NewGuid(), cancellationToken);

        Assert.Null(customer);
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistAndReturnCustomer()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new CustomerHandler(context);
        var request = new Create
        {
            Name = "Selina Kyle",
            Email = "selina@catwoman.com",
            Phone = "+55 11 95555-4444",
            BirthDate = new DateTime(1993, 7, 17, 0, 0, 0, DateTimeKind.Utc)
        };

        var response = await handler.CreateAsync(request, cancellationToken);

        var customer = await context.Customers.SingleAsync(cancellationToken);
        Assert.Equal(customer.Id, response.Id);
        Assert.Equal("Selina Kyle", response.Name);
        Assert.Equal("selina@catwoman.com", response.Email);
        Assert.Equal("Selina Kyle", customer.Name);
        Assert.Equal("selina@catwoman.com", customer.Email);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenCustomerIsMissing()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new CustomerHandler(context);
        var request = new Update
        {
            Name = "Dick Grayson",
            Email = "nightwing@bludhaven.gov",
            Phone = "+55 11 94444-3333",
            BirthDate = new DateTime(1990, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var response = await handler.UpdateAsync(Guid.NewGuid(), request, cancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var customer = new Customer
        {
            Id = Guid.Parse("0612ae76-662d-4bf8-a0a0-b3f447798740"),
            Name = "Original Name",
            Email = "original@wayne.com",
            Phone = "+55 11 90000-1234",
            BirthDate = new DateTime(1985, 2, 2, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new CustomerHandler(context);
        var request = new Update
        {
            Name = "Updated Name",
            Email = "updated@wayne.com",
            Phone = "+55 11 97777-2222",
            BirthDate = new DateTime(1985, 12, 12, 0, 0, 0, DateTimeKind.Utc)
        };

        var response = await handler.UpdateAsync(customer.Id, request, cancellationToken);

        Assert.NotNull(response);
        Assert.Equal("Updated Name", response.Name);
        var persisted = await context.Customers.SingleAsync(cancellationToken);
        Assert.Equal("Updated Name", persisted.Name);
        Assert.Equal(new DateTime(1985, 12, 12, 0, 0, 0, DateTimeKind.Utc), persisted.BirthDate);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenCustomerNotFound()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new CustomerHandler(context);

        var deleted = await handler.DeleteAsync(Guid.NewGuid(), cancellationToken);

        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveCustomer()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "To Remove",
            Email = "remove@wayne.com",
            Phone = "+55 11 91111-0000",
            BirthDate = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new CustomerHandler(context);

        var deleted = await handler.DeleteAsync(customer.Id, cancellationToken);

        Assert.True(deleted);
        Assert.Empty(context.Customers);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnAllCustomers_WhenTermIsNull()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedCustomersAsync(context, cancellationToken);
        var handler = new CustomerHandler(context);

        var result = await handler.SearchAsync(null, cancellationToken: cancellationToken);

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByPartialMatchAcrossFields()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Customers.AddRange(
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Dick Grayson",
                Email = "nightwing@gcpd.gov",
                Phone = "+55 11 90000-0000",
                BirthDate = new DateTime(1990, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Jason Todd",
                Email = "redhood@batfamily.org",
                Phone = "+55 11 95555-5555",
                BirthDate = new DateTime(1995, 8, 16, 0, 0, 0, DateTimeKind.Utc)
            });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new CustomerHandler(context);

        var matchesByName = await handler.SearchAsync("Dick", cancellationToken: cancellationToken);
        var matchesByEmail = await handler.SearchAsync("batfamily", cancellationToken: cancellationToken);
        var matchesByPhone = await handler.SearchAsync("5555", cancellationToken: cancellationToken);

        Assert.Single(matchesByName.Items);
        Assert.Equal("Dick Grayson", matchesByName.Items[0].Name);
        Assert.Single(matchesByEmail.Items);
        Assert.Equal("Jason Todd", matchesByEmail.Items[0].Name);
        Assert.Single(matchesByPhone.Items);
        Assert.Equal("Jason Todd", matchesByPhone.Items[0].Name);
    }

    [Fact]
    public async Task SearchAsync_ShouldTreatPercentAndUnderscoreAsLiterals()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Customers.AddRange(
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Match 100%",
                Email = "match@wayne.com",
                Phone = "+55 11 99999-9999",
                BirthDate = new DateTime(1985, 5, 5, 0, 0, 0, DateTimeKind.Utc)
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "No Match Here",
                Email = "nomatch@wayne.com",
                Phone = "+55 11 98888-8888",
                BirthDate = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new CustomerHandler(context);

        var result = await handler.SearchAsync("100%", cancellationToken: cancellationToken);

        Assert.Single(result.Items);
        Assert.Equal("Match 100%", result.Items[0].Name);
    }

    [Fact]
    public async Task SearchAsync_ShouldRespectPagination()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        for (var i = 1; i <= 30; i++)
        {
            context.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                Name = $"Customer {i:00}",
                Email = $"customer{i:00}@example.com",
                Phone = $"+55 11 9{i:0000}-0000",
                BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        await context.SaveChangesAsync(cancellationToken);
        var handler = new CustomerHandler(context);

        var firstPage = await handler.SearchAsync(null, page: 1, pageSize: 10, cancellationToken: cancellationToken);
        var thirdPage = await handler.SearchAsync(null, page: 3, pageSize: 10, cancellationToken: cancellationToken);

        Assert.Equal(30, firstPage.Total);
        Assert.Equal(10, firstPage.Items.Count);
        Assert.Equal(10, thirdPage.Items.Count);
        Assert.Equal("Customer 01", firstPage.Items[0].Name);
        Assert.Equal("Customer 21", thirdPage.Items[0].Name);
    }

    [Fact]
    public async Task SearchAsync_ShouldSortByEmailDescending()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Customers.AddRange(
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "First",
                Email = "a@example.com",
                Phone = "1",
                BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Second",
                Email = "c@example.com",
                Phone = "2",
                BirthDate = new DateTime(1992, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Third",
                Email = "b@example.com",
                Phone = "3",
                BirthDate = new DateTime(1994, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new CustomerHandler(context);

        var result = await handler.SearchAsync(null, sortBy: "email", sortOrder: "desc", cancellationToken: cancellationToken);

        Assert.Equal(new[] { "c@example.com", "b@example.com", "a@example.com" }, result.Items.Select(x => x.Email));
    }

    [Fact]
    public async Task SearchAsync_ShouldSortByNameDescending_WhenRequested()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Customers.AddRange(
            new Customer { Id = Guid.NewGuid(), Name = "Charlie", Email = "charlie@example.com", Phone = "1", BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Customer { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com", Phone = "2", BirthDate = new DateTime(1991, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Customer { Id = Guid.NewGuid(), Name = "Bob", Email = "bob@example.com", Phone = "3", BirthDate = new DateTime(1992, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new CustomerHandler(context);

        var result = await handler.SearchAsync(null, sortBy: "name", sortOrder: "desc", cancellationToken: cancellationToken);

        Assert.Equal(new[] { "Charlie", "Bob", "Alice" }, result.Items.Select(x => x.Name));
    }

    [Fact]
    public async Task SearchAsync_ShouldFallbackToName_WhenSortByIsUnknown()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Customers.AddRange(
            new Customer { Id = Guid.NewGuid(), Name = "Gamma", Email = "g@example.com", Phone = "1", BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Customer { Id = Guid.NewGuid(), Name = "Alpha", Email = "a@example.com", Phone = "2", BirthDate = new DateTime(1991, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Customer { Id = Guid.NewGuid(), Name = "Beta", Email = "b@example.com", Phone = "3", BirthDate = new DateTime(1992, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new CustomerHandler(context);

        var result = await handler.SearchAsync(null, sortBy: "unsupported", sortOrder: "asc", cancellationToken: cancellationToken);

        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, result.Items.Select(x => x.Name));
    }

    private static async Task SeedCustomersAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        context.Customers.AddRange(
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Harvey Bullock",
                Email = "harvey@gcpd.gov",
                Phone = "+55 11 93333-2222",
                BirthDate = new DateTime(1975, 5, 5, 0, 0, 0, DateTimeKind.Utc)
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Barbara Gordon",
                Email = "barbara@gcpd.gov",
                Phone = "+55 11 92222-1111",
                BirthDate = new DateTime(1988, 9, 23, 0, 0, 0, DateTimeKind.Utc)
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Alfred Pennyworth",
                Email = "alfred@wayne.com",
                Phone = "+55 11 98888-0000",
                BirthDate = new DateTime(1943, 4, 29, 0, 0, 0, DateTimeKind.Utc)
            });

        await context.SaveChangesAsync(cancellationToken);
    }
}
