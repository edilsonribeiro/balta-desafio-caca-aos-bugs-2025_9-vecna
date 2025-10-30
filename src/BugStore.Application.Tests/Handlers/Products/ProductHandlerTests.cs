using System.Linq;
using BugStore.Application.Handlers.Products;
using BugStore.Application.Requests.Products;
using BugStore.Application.Tests.Support;
using BugStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BugStore.Application.Tests.Handlers.Products;

public sealed class ProductHandlerTests
{
    [Fact]
    public async Task GetAsync_ShouldReturnProductsOrderedByTitle()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Grappling Gun",
                Description = "Portable grappling gun",
                Slug = "grappling-gun",
                Price = 799.99m
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Batarang",
                Description = "Standard batarang set",
                Slug = "batarang",
                Price = 299.99m
            });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);

        var products = await handler.GetAsync(cancellationToken);

        Assert.Collection(products,
            product => Assert.Equal("Batarang", product.Title),
            product => Assert.Equal("Grappling Gun", product.Title));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnProduct_WhenExists()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Smoke Pellet",
            Description = "Pellet for quick escapes",
            Slug = "smoke-pellet",
            Price = 49.99m
        };
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);

        var response = await handler.GetByIdAsync(product.Id, cancellationToken);

        Assert.NotNull(response);
        Assert.Equal(product.Id, response.Id);
        Assert.Equal("Smoke Pellet", response.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenMissing()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ProductHandler(context);

        var response = await handler.GetByIdAsync(Guid.NewGuid(), cancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistProduct()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ProductHandler(context);
        var request = new Create
        {
            Title = "Batmobile Maintenance Kit",
            Description = "Full toolkit for Batmobile",
            Slug = "batmobile-maintenance-kit",
            Price = 1200m
        };

        var response = await handler.CreateAsync(request, cancellationToken);

        var persisted = await context.Products.SingleAsync(cancellationToken);
        Assert.Equal(response.Id, persisted.Id);
        Assert.Equal("Batmobile Maintenance Kit", response.Title);
        Assert.Equal("batmobile-maintenance-kit", persisted.Slug);
        Assert.Equal(1200m, persisted.Price);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenProductNotFound()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ProductHandler(context);
        var request = new Update
        {
            Title = "Updated Title",
            Description = "Updated description",
            Slug = "updated-slug",
            Price = 1500m
        };

        var response = await handler.UpdateAsync(Guid.NewGuid(), request, cancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Original Title",
            Description = "Original description",
            Slug = "original-slug",
            Price = 750m
        };
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);
        var request = new Update
        {
            Title = "Revised Title",
            Description = "Revised description",
            Slug = "revised-slug",
            Price = 800m
        };

        var response = await handler.UpdateAsync(product.Id, request, cancellationToken);

        Assert.NotNull(response);
        Assert.Equal("Revised Title", response.Title);
        var persisted = await context.Products.SingleAsync(cancellationToken);
        Assert.Equal("revised-slug", persisted.Slug);
        Assert.Equal(800m, persisted.Price);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnNotFound_WhenProductMissing()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ProductHandler(context);

        var result = await handler.DeleteAsync(Guid.NewGuid(), cancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveProduct()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Disruptor",
            Description = "Electronic disruptor",
            Slug = "disruptor",
            Price = 299.99m
        };
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);

        var result = await handler.DeleteAsync(product.Id, cancellationToken);

        Assert.True(result);
        Assert.Empty(context.Products);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveRelatedOrderLines()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Explosive Gel",
            Description = "Remote explosive gel",
            Slug = "explosive-gel",
            Price = 199.99m
        };
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Jason Todd",
            Email = "jason@batfamily.com",
            Phone = "+55 11 98888-7777",
            BirthDate = new DateTime(1990, 8, 16, 0, 0, 0, DateTimeKind.Utc)
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
                    OrderId = Guid.Empty,
                    ProductId = product.Id,
                    Product = product,
                    Quantity = 1,
                    Total = product.Price
                }
            }
        };
        order.Lines[0].OrderId = order.Id;

        context.Customers.Add(customer);
        context.Products.Add(product);
        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);

        var result = await handler.DeleteAsync(product.Id, cancellationToken);

        Assert.True(result);
        Assert.Equal(0, await context.Products.CountAsync(cancellationToken));
        Assert.Equal(0, await context.OrderLines.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnAllProducts_WhenTermIsWhitespace()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Shock Gloves",
                Description = "Conductive gloves",
                Slug = "shock-gloves",
                Price = 599.99m
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "EMP Disruptor",
                Description = "Electromagnetic pulse device",
                Slug = "emp-disruptor",
                Price = 899.99m
            });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);

        var result = await handler.SearchAsync("   ", cancellationToken: cancellationToken);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByTitleDescriptionAndSlug()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Smoke Pellet",
                Description = "Vanish into smoke",
                Slug = "smoke-pellet",
                Price = 59.99m
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Freeze Grenade",
                Description = "Ice-based gadget",
                Slug = "freeze-grenade",
                Price = 129.99m
            });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);

        var byTitle = await handler.SearchAsync("Smoke", cancellationToken: cancellationToken);
        var byDescription = await handler.SearchAsync("ice-based", cancellationToken: cancellationToken);
        var bySlug = await handler.SearchAsync("freeze-grenade", cancellationToken: cancellationToken);

        Assert.Single(byTitle.Items);
        Assert.Equal("Smoke Pellet", byTitle.Items[0].Title);
        Assert.Single(byDescription.Items);
        Assert.Equal("Freeze Grenade", byDescription.Items[0].Title);
        Assert.Single(bySlug.Items);
        Assert.Equal("Freeze Grenade", bySlug.Items[0].Title);
    }

    [Fact]
    public async Task SearchAsync_ShouldTreatWildcardsAsLiterals()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Percent% Gadget",
                Description = "Special percent gadget",
                Slug = "percent-gadget",
                Price = 199.99m
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Underscore_Device",
                Description = "Special underscore gadget",
                Slug = "underscore-device",
                Price = 149.99m
            });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);

        var percentMatches = await handler.SearchAsync("Percent%", cancellationToken: cancellationToken);
        var underscoreMatches = await handler.SearchAsync("Underscore_", cancellationToken: cancellationToken);

        Assert.Single(percentMatches.Items);
        Assert.Equal("Percent% Gadget", percentMatches.Items[0].Title);
        Assert.Single(underscoreMatches.Items);
        Assert.Equal("Underscore_Device", underscoreMatches.Items[0].Title);
    }

    [Fact]
    public async Task SearchAsync_ShouldRespectPagination()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        for (var i = 1; i <= 25; i++)
        {
            context.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                Title = $"Gadget {i:00}",
                Description = $"Description {i:00}",
                Slug = $"gadget-{i:00}",
                Price = 100 + i
            });
        }
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);

        var firstPage = await handler.SearchAsync(null, page: 1, pageSize: 10, cancellationToken: cancellationToken);
        var thirdPage = await handler.SearchAsync(null, page: 3, pageSize: 10, cancellationToken: cancellationToken);

        Assert.Equal(25, firstPage.Total);
        Assert.Equal(10, firstPage.Items.Count);
        Assert.Equal(5, thirdPage.Items.Count);
        Assert.Equal("Gadget 01", firstPage.Items[0].Title);
        Assert.Equal("Gadget 21", thirdPage.Items[0].Title);
    }

    [Fact]
    public async Task SearchAsync_ShouldSortByPriceAscending()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Expensive",
                Description = "High cost",
                Slug = "expensive",
                Price = 300m
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Cheap",
                Description = "Low cost",
                Slug = "cheap",
                Price = 50m
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Title = "Medium",
                Description = "Mid cost",
                Slug = "medium",
                Price = 150m
            });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);

        var result = await handler.SearchAsync(null, sortBy: "price", sortOrder: "asc", cancellationToken: cancellationToken);

        Assert.Equal(new[] { 50m, 150m, 300m }, result.Items.Select(x => x.Price));
    }

    [Fact]
    public async Task SearchAsync_ShouldSortBySlugDescending()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Title = "One", Description = "One", Slug = "slug-a", Price = 10m },
            new Product { Id = Guid.NewGuid(), Title = "Two", Description = "Two", Slug = "slug-c", Price = 20m },
            new Product { Id = Guid.NewGuid(), Title = "Three", Description = "Three", Slug = "slug-b", Price = 30m });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);

        var result = await handler.SearchAsync(null, sortBy: "slug", sortOrder: "desc", cancellationToken: cancellationToken);

        Assert.Equal(new[] { "slug-c", "slug-b", "slug-a" }, result.Items.Select(x => x.Slug));
    }

    [Fact]
    public async Task SearchAsync_ShouldFallbackToTitleAscending_WhenSortByUnsupported()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        context.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Title = "Gamma", Description = "Gamma", Slug = "g", Price = 30m },
            new Product { Id = Guid.NewGuid(), Title = "Alpha", Description = "Alpha", Slug = "a", Price = 10m },
            new Product { Id = Guid.NewGuid(), Title = "Beta", Description = "Beta", Slug = "b", Price = 20m });
        await context.SaveChangesAsync(cancellationToken);
        var handler = new ProductHandler(context);

        var result = await handler.SearchAsync(null, sortBy: "unknown", sortOrder: "asc", cancellationToken: cancellationToken);

        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, result.Items.Select(x => x.Title));
    }
}
