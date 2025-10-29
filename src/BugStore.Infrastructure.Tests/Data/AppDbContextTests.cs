using BugStore.Domain.Entities;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BugStore.Infrastructure.Tests.Data;

public sealed class AppDbContextTests
{
    [Fact]
    public async Task DbSets_ShouldPersistEntities()
    {
        var options = BuildOptions();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var context = new AppDbContext(options))
        {
            context.Customers.Add(new Customer
            {
                Id = Guid.Parse("3e9b0a14-9c35-4b7d-8c41-4d4e905f7abe"),
                Name = "Clark Kent",
                Email = "clark@dailyplanet.com",
                Phone = "+55 11 98888-7777",
                BirthDate = new DateTime(1986, 6, 18, 0, 0, 0, DateTimeKind.Utc)
            });

            context.Products.Add(new Product
            {
                Id = Guid.Parse("6a0320ee-8356-4c71-9c19-5b153dca8ae7"),
                Title = "Kryptonite Detector",
                Description = "Detects kryptonite in a six block radius",
                Slug = "kryptonite-detector",
                Price = 999.99m
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var verificationContext = new AppDbContext(options))
        {
            var customer = await verificationContext.Customers.SingleAsync(cancellationToken);
            var product = await verificationContext.Products.SingleAsync(cancellationToken);

            Assert.Equal("Clark Kent", customer.Name);
            Assert.Equal("kryptonite-detector", product.Slug);
        }
    }

    [Fact]
    public async Task OrderGraph_ShouldRoundtripThroughContext()
    {
        var options = BuildOptions();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var context = new AppDbContext(options))
        {
            context.Customers.Add(new Customer
            {
                Id = customerId,
                Name = "Diana Prince",
                Email = "diana@themiscira.gov",
                Phone = "+55 11 97777-6666",
                BirthDate = new DateTime(1980, 3, 22, 0, 0, 0, DateTimeKind.Utc)
            });

            context.Products.Add(new Product
            {
                Id = productId,
                Title = "Lasso of Truth Holder",
                Description = "Custom mount for the lasso of truth",
                Slug = "lasso-holder",
                Price = 150.0m
            });

            context.Orders.Add(new Order
            {
                Id = orderId,
                CustomerId = customerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Lines =
                {
                    new OrderLine
                    {
                        Id = lineId,
                        OrderId = orderId,
                        ProductId = productId,
                        Quantity = 2,
                        Total = 300.0m
                    }
                }
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        await using var verificationContext = new AppDbContext(options);
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Lines)
            .SingleAsync(order => order.Id == orderId, cancellationToken);

        Assert.Equal(customerId, persistedOrder.CustomerId);
        Assert.Single(persistedOrder.Lines);
        Assert.Equal(lineId, persistedOrder.Lines[0].Id);
        Assert.Equal(300.0m, persistedOrder.Lines[0].Total);
    }

    private static DbContextOptions<AppDbContext> BuildOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }
}
