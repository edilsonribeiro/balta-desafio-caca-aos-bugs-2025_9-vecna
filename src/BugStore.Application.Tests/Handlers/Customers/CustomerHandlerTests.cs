using AutoMapper;
using BugStore.Application.Caching;
using BugStore.Application.Handlers.Customers.Commands;
using BugStore.Application.Handlers.Customers.Queries;
using BugStore.Application.Mapping.Profiles;
using BugStore.Application.Requests.Customers;
using BugStore.Application.Tests.Support;
using BugStore.Domain.Entities;
using BugStore.Infrastructure.Data;
using BugStore.Infrastructure.Repositories.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BugStore.Application.Tests.Handlers.Customers;

public sealed class CustomerHandlerTests
{
    private static readonly IMapper Mapper = new MapperConfiguration(configuration =>
        configuration.AddProfile<CustomerProfile>())
        .CreateMapper();

    [Fact]
    public async Task Search_ShouldReturnCustomersOrderedByName()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedCustomersAsync(context, cancellationToken);
        using var cache = CreateCache();
        var handler = CreateQueryHandler(context, cache);

        var query = new SearchCustomersQuery(null, SortBy: "name", SortOrder: "asc");
        var result = await handler.Handle(query, cancellationToken);

        Assert.Equal(3, result.Items.Count);
        Assert.Collection(result.Items,
            customer => Assert.Equal("Alfred Pennyworth", customer.Name),
            customer => Assert.Equal("Barbara Gordon", customer.Name),
            customer => Assert.Equal("Harvey Bullock", customer.Name));
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturnMatchingCustomer()
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
        using var cache = CreateCache();
        var handler = CreateQueryHandler(context, cache);

        var query = new GetCustomerByIdQuery(customerId);
        var customer = await handler.Handle(query, cancellationToken);

        Assert.NotNull(customer);
        Assert.Equal(customerId, customer.Id);
        Assert.Equal("Lucius Fox", customer.Name);
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturnNull_WhenCustomerDoesNotExist()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var cache = CreateCache();
        var handler = CreateQueryHandler(context, cache);

        var query = new GetCustomerByIdQuery(Guid.NewGuid());
        var customer = await handler.Handle(query, cancellationToken);

        Assert.Null(customer);
    }

    [Fact]
    public async Task CreateCustomer_ShouldPersistAndReturnCustomer()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = CreateCommandHandler(context);
        var request = new Create
        {
            Name = "Selina Kyle",
            Email = "selina@catwoman.com",
            Phone = "+55 11 95555-4444",
            BirthDate = new DateTime(1993, 7, 17, 0, 0, 0, DateTimeKind.Utc)
        };

        var command = new CreateCustomerCommand(request);
        var response = await handler.Handle(command, cancellationToken);

        var customer = await context.Customers.SingleAsync(cancellationToken);
        Assert.Equal(customer.Id, response.Id);
        Assert.Equal("Selina Kyle", response.Name);
        Assert.Equal("selina@catwoman.com", response.Email);
        Assert.Equal("Selina Kyle", customer.Name);
        Assert.Equal("selina@catwoman.com", customer.Email);
    }

    [Fact]
    public async Task UpdateCustomer_ShouldReturnNull_WhenCustomerIsMissing()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = CreateCommandHandler(context);
        var request = new Update
        {
            Name = "Dick Grayson",
            Email = "nightwing@bludhaven.gov",
            Phone = "+55 11 94444-3333",
            BirthDate = new DateTime(1990, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var command = new UpdateCustomerCommand(Guid.NewGuid(), request);
        var response = await handler.Handle(command, cancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task UpdateCustomer_ShouldPersistChanges()
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
        var handler = CreateCommandHandler(context);
        var request = new Update
        {
            Name = "Updated Name",
            Email = "updated@wayne.com",
            Phone = "+55 11 97777-2222",
            BirthDate = new DateTime(1985, 12, 12, 0, 0, 0, DateTimeKind.Utc)
        };

        var command = new UpdateCustomerCommand(customer.Id, request);
        var response = await handler.Handle(command, cancellationToken);

        Assert.NotNull(response);
        Assert.Equal("Updated Name", response.Name);
        var persisted = await context.Customers.SingleAsync(cancellationToken);
        Assert.Equal("Updated Name", persisted.Name);
        Assert.Equal(new DateTime(1985, 12, 12, 0, 0, 0, DateTimeKind.Utc), persisted.BirthDate);
    }

    [Fact]
    public async Task DeleteCustomer_ShouldReturnFalse_WhenCustomerNotFound()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = CreateCommandHandler(context);

        var command = new DeleteCustomerCommand(Guid.NewGuid());
        var deleted = await handler.Handle(command, cancellationToken);

        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteCustomer_ShouldRemoveCustomer()
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
        var handler = CreateCommandHandler(context);

        var command = new DeleteCustomerCommand(customer.Id);
        var deleted = await handler.Handle(command, cancellationToken);

        Assert.True(deleted);
        Assert.Empty(context.Customers);
    }

    [Fact]
    public async Task Search_ShouldReturnAllCustomers_WhenTermIsNull()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedCustomersAsync(context, cancellationToken);
        using var cache = CreateCache();
        var handler = CreateQueryHandler(context, cache);

        var query = new SearchCustomersQuery(null);
        var result = await handler.Handle(query, cancellationToken);

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task Search_ShouldFilterByPartialMatchAcrossFields()
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
        using var cache = CreateCache();
        var handler = CreateQueryHandler(context, cache);

        var matchesByName = await handler.Handle(new SearchCustomersQuery("Dick"), cancellationToken);
        var matchesByEmail = await handler.Handle(new SearchCustomersQuery("batfamily"), cancellationToken);
        var matchesByPhone = await handler.Handle(new SearchCustomersQuery("5555"), cancellationToken);

        Assert.Single(matchesByName.Items);
        Assert.Equal("Dick Grayson", matchesByName.Items[0].Name);
        Assert.Single(matchesByEmail.Items);
        Assert.Equal("Jason Todd", matchesByEmail.Items[0].Name);
        Assert.Single(matchesByPhone.Items);
        Assert.Equal("Jason Todd", matchesByPhone.Items[0].Name);
    }

    [Fact]
    public async Task Search_ShouldTreatPercentAndUnderscoreAsLiterals()
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
        using var cache = CreateCache();
        var handler = CreateQueryHandler(context, cache);

        var result = await handler.Handle(new SearchCustomersQuery("100%"), cancellationToken);

        Assert.Single(result.Items);
        Assert.Equal("Match 100%", result.Items[0].Name);
    }

    [Fact]
    public async Task Search_ShouldRespectPagination()
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
        using var cache = CreateCache();
        var handler = CreateQueryHandler(context, cache);

        var firstPage = await handler.Handle(new SearchCustomersQuery(null, Page: 1, PageSize: 10), cancellationToken);
        var thirdPage = await handler.Handle(new SearchCustomersQuery(null, Page: 3, PageSize: 10), cancellationToken);

        Assert.Equal(30, firstPage.Total);
        Assert.Equal(10, firstPage.Items.Count);
        Assert.Equal(10, thirdPage.Items.Count);
        Assert.Equal("Customer 01", firstPage.Items[0].Name);
        Assert.Equal("Customer 21", thirdPage.Items[0].Name);
    }

    [Fact]
    public async Task Search_ShouldSortByEmailDescending()
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
        using var cache = CreateCache();
        var handler = CreateQueryHandler(context, cache);

        var result = await handler.Handle(new SearchCustomersQuery(null, SortBy: "email", SortOrder: "desc"), cancellationToken);

        Assert.Equal(new[] { "c@example.com", "b@example.com", "a@example.com" }, result.Items.Select(x => x.Email));
    }

    [Fact]
    public async Task Search_ShouldSortByBirthDateDescending()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Customers.AddRange(
            new Customer { Id = Guid.NewGuid(), Name = "Charlie", Email = "charlie@example.com", Phone = "1", BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Customer { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com", Phone = "2", BirthDate = new DateTime(1993, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Customer { Id = Guid.NewGuid(), Name = "Bob", Email = "bob@example.com", Phone = "3", BirthDate = new DateTime(1991, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        await context.SaveChangesAsync(cancellationToken);
        using var cache = CreateCache();
        var handler = CreateQueryHandler(context, cache);

        var result = await handler.Handle(new SearchCustomersQuery(null, SortBy: "birthdate", SortOrder: "desc"), cancellationToken);

        Assert.Equal(new[] { new DateTime(1993, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(1991, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, result.Items.Select(x => x.BirthDate));
    }

    [Fact]
    public async Task Search_ShouldFallbackToName_WhenSortByIsUnknown()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Customers.AddRange(
            new Customer { Id = Guid.NewGuid(), Name = "Gamma", Email = "g@example.com", Phone = "1", BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Customer { Id = Guid.NewGuid(), Name = "Alpha", Email = "a@example.com", Phone = "2", BirthDate = new DateTime(1991, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Customer { Id = Guid.NewGuid(), Name = "Beta", Email = "b@example.com", Phone = "3", BirthDate = new DateTime(1992, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        await context.SaveChangesAsync(cancellationToken);
        using var cache = CreateCache();
        var handler = CreateQueryHandler(context, cache);

        var result = await handler.Handle(new SearchCustomersQuery(null, SortBy: "unsupported", SortOrder: "asc"), cancellationToken);

        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, result.Items.Select(x => x.Name));
    }

    private static CustomerCommandHandler CreateCommandHandler(AppDbContext context, ICustomerCacheSignal? cacheSignal = null)
    {
        cacheSignal ??= new CustomerCacheSignal();
        var repository = new CustomerRepository(context);
        var unitOfWork = new UnitOfWork(context);
        return new CustomerCommandHandler(repository, unitOfWork, Mapper, cacheSignal);
    }

    private static CustomerQueryHandler CreateQueryHandler(AppDbContext context, IMemoryCache cache, ICustomerCacheSignal? cacheSignal = null)
    {
        var repository = new CustomerRepository(context);
        return new CustomerQueryHandler(repository, Mapper, cache, cacheSignal ?? new CustomerCacheSignal());
    }

    private static MemoryCache CreateCache() => new(new MemoryCacheOptions());

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
