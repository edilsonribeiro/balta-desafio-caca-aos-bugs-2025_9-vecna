using System.Linq;
using BugStore.Application.Handlers.Orders;
using BugStore.Application.Requests.Orders;
using BugStore.Application.Tests.Support;
using BugStore.Domain.Entities;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BugStore.Application.Tests.Handlers.Orders;

public sealed class OrderHandlerTests
{
    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenOrderMissing()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new OrderHandler(context);

        var response = await handler.GetByIdAsync(Guid.NewGuid(), cancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldMapOrderWithLines()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Commissioner Gordon",
            Email = "gordon@gcpd.gov",
            Phone = "+55 11 92222-0000",
            BirthDate = new DateTime(1955, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Bat Signal Bulb",
            Description = "Replacement bulb for the Bat Signal",
            Slug = "bat-signal-bulb",
            Price = 500m
        };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
            Lines =
            {
                new OrderLine
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Quantity = 3,
                    Total = 1500m
                }
            }
        };

        context.Customers.Add(customer);
        context.Products.Add(product);
        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new OrderHandler(context);

        var response = await handler.GetByIdAsync(order.Id, cancellationToken);

        Assert.NotNull(response);
        Assert.Equal(order.Id, response.Id);
        Assert.Equal(1500m, response.Total);
        Assert.Single(response.Lines);
        Assert.Equal(product.Title, response.Lines[0].ProductTitle);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNull_WhenLinesAreNull()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new OrderHandler(context);
        var request = new Create
        {
            CustomerId = Guid.NewGuid(),
            Lines = null!
        };

        var response = await handler.CreateAsync(request, cancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNull_WhenLinesAreEmpty()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new OrderHandler(context);
        var request = new Create
        {
            CustomerId = Guid.NewGuid(),
            Lines = []
        };

        var response = await handler.CreateAsync(request, cancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNull_WhenAnyQuantityIsInvalid()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new OrderHandler(context);
        var request = new Create
        {
            CustomerId = Guid.NewGuid(),
            Lines =
            [
                new Create.Line(Guid.NewGuid(), 0)
            ]
        };

        var response = await handler.CreateAsync(request, cancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNull_WhenCustomerDoesNotExist()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Explosive Gel",
            Description = "High precision explosive gel",
            Slug = "explosive-gel",
            Price = 220m
        };
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new OrderHandler(context);
        var request = new Create
        {
            CustomerId = Guid.NewGuid(),
            Lines =
            [
                new Create.Line(product.Id, 1)
            ]
        };

        var response = await handler.CreateAsync(request, cancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNull_WhenAnyProductIsMissing()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Leslie Thompkins",
            Email = "leslie@gothamcare.org",
            Phone = "+55 11 98888-2222",
            BirthDate = new DateTime(1950, 8, 8, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new OrderHandler(context);
        var request = new Create
        {
            CustomerId = customer.Id,
            Lines =
            [
                new Create.Line(Guid.NewGuid(), 2)
            ]
        };

        var response = await handler.CreateAsync(request, cancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistOrderAndReturnResponse()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Bruce Wayne",
            Email = "bruce@wayneenterprises.com",
            Phone = "+55 11 99999-9999",
            BirthDate = new DateTime(1972, 2, 19, 0, 0, 0, DateTimeKind.Utc)
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Bat Suit Upgrade",
            Description = "Latest Kevlar upgrade",
            Slug = "bat-suit-upgrade",
            Price = 5000m
        };
        context.Customers.Add(customer);
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new OrderHandler(context);
        var request = new Create
        {
            CustomerId = customer.Id,
            Lines =
            [
                new Create.Line(product.Id, 2)
            ]
        };
        var before = DateTime.UtcNow;

        var response = await handler.CreateAsync(request, cancellationToken);

        Assert.NotNull(response);
        Assert.Equal(customer.Id, response.CustomerId);
        Assert.Single(response.Lines);
        Assert.Equal(10000m, response.Total);
        Assert.Equal(10000m, response.Lines[0].Total);
        Assert.InRange(response.CreatedAt, before, DateTime.UtcNow);
        Assert.InRange(response.UpdatedAt, before, DateTime.UtcNow);

        var persistedOrder = await context.Orders.Include(order => order.Lines)
            .SingleAsync(cancellationToken);
        Assert.Equal(response.Id, persistedOrder.Id);
        Assert.Equal(2, persistedOrder.Lines[0].Quantity);
        Assert.Equal(10000m, persistedOrder.Lines[0].Total);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnAllOrders_WhenTermIsNull()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedOrdersAsync(context, cancellationToken);
        var handler = new OrderHandler(context);

        var result = await handler.SearchAsync(null, cancellationToken: cancellationToken);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByCustomerData()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedOrdersAsync(context, cancellationToken);
        var handler = new OrderHandler(context);

        var byName = await handler.SearchAsync("Selina", cancellationToken: cancellationToken);
        var byEmail = await handler.SearchAsync("wayneenterprises", cancellationToken: cancellationToken);
        var byPhone = await handler.SearchAsync("90000", cancellationToken: cancellationToken);

        Assert.Single(byName.Items);
        Assert.Equal("Selina Kyle", byName.Items[0].CustomerName);
        Assert.Single(byEmail.Items);
        Assert.Equal("Bruce Wayne", byEmail.Items[0].CustomerName);
        Assert.Single(byPhone.Items);
        Assert.Equal("Bruce Wayne", byPhone.Items[0].CustomerName);
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByProductInformation()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedOrdersAsync(context, cancellationToken);
        var handler = new OrderHandler(context);

        var byTitle = await handler.SearchAsync("Batarang", cancellationToken: cancellationToken);
        var byDescription = await handler.SearchAsync("grappling", cancellationToken: cancellationToken);
        var bySlug = await handler.SearchAsync("grappling-gun", cancellationToken: cancellationToken);

        Assert.Single(byTitle.Items);
        Assert.Equal("Bruce Wayne", byTitle.Items[0].CustomerName);
        Assert.Single(byDescription.Items);
        Assert.Equal("Selina Kyle", byDescription.Items[0].CustomerName);
        Assert.Single(bySlug.Items);
        Assert.Equal("Selina Kyle", bySlug.Items[0].CustomerName);
    }

    [Fact]
    public async Task SearchAsync_ShouldMatchGuidForOrderOrCustomer()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedOrdersAsync(context, cancellationToken);
        var handler = new OrderHandler(context);

        var byOrderId = await handler.SearchAsync(data.Order1.Id.ToString(), cancellationToken: cancellationToken);
        var byCustomerId = await handler.SearchAsync(data.Customer2.Id.ToString(), cancellationToken: cancellationToken);

        Assert.Single(byOrderId.Items);
        Assert.Equal(data.Order1.Id, byOrderId.Items[0].Id);
        Assert.Single(byCustomerId.Items);
        Assert.Equal(data.Customer2.Id, byCustomerId.Items[0].CustomerId);
    }

    [Fact]
    public async Task SearchAsync_ShouldTreatWildcardsAsLiterals()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Match 100%",
            Email = "match@wayne.com",
            Phone = "+55 11 94444-4444",
            BirthDate = new DateTime(1984, 4, 4, 0, 0, 0, DateTimeKind.Utc)
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Percent% Gadget",
            Description = "Literal percent gadget",
            Slug = "percent-gadget",
            Price = 10m
        };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines =
            {
                new OrderLine
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Product = product,
                    Quantity = 1,
                    Total = 10m
                }
            }
        };
        context.Customers.Add(customer);
        context.Products.Add(product);
        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new OrderHandler(context);

        var result = await handler.SearchAsync("100%", cancellationToken: cancellationToken);

        Assert.Single(result.Items);
        Assert.Equal("Match 100%", result.Items[0].CustomerName);
    }

    [Fact]
    public async Task SearchAsync_ShouldRespectPagination()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedOrdersAsync(context, cancellationToken);
        var extraOrder = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = data.Customer1.Id,
            Customer = data.Customer1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines =
            {
                new OrderLine
                {
                    Id = Guid.NewGuid(),
                    ProductId = data.Order1.Lines[0].ProductId,
                    Product = data.Order1.Lines[0].Product,
                    Quantity = 1,
                    Total = 200m
                }
            }
        };
        context.Orders.Add(extraOrder);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new OrderHandler(context);

        var firstPage = await handler.SearchAsync(null, page: 1, pageSize: 1, cancellationToken: cancellationToken);
        var secondPage = await handler.SearchAsync(null, page: 2, pageSize: 1, cancellationToken: cancellationToken);
        var thirdPage = await handler.SearchAsync(null, page: 3, pageSize: 1, cancellationToken: cancellationToken);

        Assert.Equal(3, firstPage.Total);
        Assert.Single(firstPage.Items);
        Assert.Single(secondPage.Items);
        Assert.Single(thirdPage.Items);
        Assert.NotEqual(firstPage.Items[0].Id, secondPage.Items[0].Id);
        Assert.NotEqual(secondPage.Items[0].Id, thirdPage.Items[0].Id);
        Assert.NotEqual(firstPage.Items[0].Id, thirdPage.Items[0].Id);
    }

    [Fact]
    public async Task SearchAsync_ShouldSortByTotalAscending()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Sorter",
            Email = "sorter@example.com",
            Phone = "1",
            BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Item",
            Description = "Item desc",
            Slug = "item",
            Price = 100m
        };
        context.Customers.Add(customer);
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);
        var order1 = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            CreatedAt = DateTime.UtcNow.AddMinutes(-3),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-3),
            Lines =
            {
                new OrderLine
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Product = product,
                    Quantity = 1,
                    Total = 100m
                }
            }
        };
        var order2 = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-2),
            Lines =
            {
                new OrderLine
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Product = product,
                    Quantity = 3,
                    Total = 300m
                }
            }
        };
        var order3 = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
            Lines =
            {
                new OrderLine
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Product = product,
                    Quantity = 2,
                    Total = 200m
                }
            }
        };

        context.Orders.AddRange(order1, order2, order3);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new OrderHandler(context);

        var result = await handler.SearchAsync(null, sortBy: "total", sortOrder: "asc", cancellationToken: cancellationToken);

        Assert.Equal(new[] { 100m, 200m, 300m }, result.Items.Select(x => x.Total));
    }

    [Fact]
    public async Task SearchAsync_ShouldSortByCreatedAtDescending_ByDefault()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Default",
            Email = "default@example.com",
            Phone = "1",
            BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Item",
            Description = "Item",
            Slug = "item",
            Price = 100m
        };
        context.Customers.Add(customer);
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);

        var older = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
            Lines =
            {
                new OrderLine
                {
                    Id = Guid.NewGuid(),
                    OrderId = Guid.Empty, // placeholder
                    ProductId = product.Id,
                    Product = product,
                    Quantity = 1,
                    Total = 100m
                }
            }
        };
        older.Lines[0].OrderId = older.Id;

        var newer = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
            Lines =
            {
                new OrderLine
                {
                    Id = Guid.NewGuid(),
                    OrderId = Guid.Empty,
                    ProductId = product.Id,
                    Product = product,
                    Quantity = 2,
                    Total = 200m
                }
            }
        };
        newer.Lines[0].OrderId = newer.Id;

        context.Orders.AddRange(older, newer);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new OrderHandler(context);

        var result = await handler.SearchAsync(null, cancellationToken: cancellationToken);

        Assert.Equal(new[] { newer.Id, older.Id }, result.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task SearchAsync_ShouldFallbackToCreatedAt_WhenSortByUnsupported()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedOrdersAsync(context, cancellationToken);
        var handler = new OrderHandler(context);

        var result = await handler.SearchAsync(null, sortBy: "invalid", sortOrder: "asc", cancellationToken: cancellationToken);

        Assert.Equal(new[] { data.Order2.Id, data.Order1.Id }, result.Items.Select(x => x.Id));
    }

    private static async Task<(Customer Customer1, Customer Customer2, Order Order1, Order Order2)> SeedOrdersAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var customer1 = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Bruce Wayne",
            Email = "bruce@wayneenterprises.com",
            Phone = "+55 11 90000-0000",
            BirthDate = new DateTime(1972, 2, 19, 0, 0, 0, DateTimeKind.Utc)
        };
        var customer2 = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Selina Kyle",
            Email = "selina@gothamcity.com",
            Phone = "+55 11 95555-1111",
            BirthDate = new DateTime(1985, 7, 17, 0, 0, 0, DateTimeKind.Utc)
        };
        var product1 = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Batarang",
            Description = "Standard bat projectile",
            Slug = "batarang",
            Price = 100m
        };
        var product2 = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Grappling Gun",
            Description = "High tensile grappling device",
            Slug = "grappling-gun",
            Price = 500m
        };
        var now = DateTime.UtcNow;
        var order1 = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer1.Id,
            Customer = customer1,
            CreatedAt = now.AddMinutes(-10),
            UpdatedAt = now.AddMinutes(-10),
            Lines =
            {
                new OrderLine
                {
                    Id = Guid.NewGuid(),
                    ProductId = product1.Id,
                    Product = product1,
                    Quantity = 2,
                    Total = 200m
                }
            }
        };
        var order2 = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer2.Id,
            Customer = customer2,
            CreatedAt = now.AddMinutes(-5),
            UpdatedAt = now.AddMinutes(-5),
            Lines =
            {
                new OrderLine
                {
                    Id = Guid.NewGuid(),
                    ProductId = product2.Id,
                    Product = product2,
                    Quantity = 1,
                    Total = 500m
                }
            }
        };

        context.Customers.AddRange(customer1, customer2);
        context.Products.AddRange(product1, product2);
        context.Orders.AddRange(order1, order2);
        await context.SaveChangesAsync(cancellationToken);

        return (customer1, customer2, order1, order2);
    }
}
