using BugStore.Application.Handlers.Reports;
using BugStore.Application.Responses.Reports;
using BugStore.Application.Tests.Support;
using BugStore.Domain.Entities;

namespace BugStore.Application.Tests.Handlers.Reports;

public sealed class ReportHandlerTests
{
    [Fact]
    public async Task GetSalesByCustomerAsync_ShouldAggregateDataPerCustomer()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var wayne = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Bruce Wayne",
            Email = "bruce@wayneenterprises.com",
            Phone = "+55 11 99999-9999",
            BirthDate = new DateTime(1980, 2, 19, 0, 0, 0, DateTimeKind.Utc)
        };
        var kent = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Clark Kent",
            Email = "clark@dailyplanet.com",
            Phone = "+1 555 700-0000",
            BirthDate = new DateTime(1978, 6, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        context.Customers.AddRange(wayne, kent);

        var reference = new DateTime(2025, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        var wayneFirstOrderAt = reference.AddDays(-3);
        var wayneSecondOrderAt = reference.AddDays(-2);
        var kentOrderAt = reference.AddDays(-1);

        context.Orders.AddRange(
            CreateOrder(wayne.Id, 2, 50m, wayneFirstOrderAt),
            CreateOrder(wayne.Id, 1, 25m, wayneSecondOrderAt),
            CreateOrder(kent.Id, 3, 30m, kentOrderAt));

        await context.SaveChangesAsync(cancellationToken);

        var handler = new ReportHandler(context);

        var result = await handler.GetSalesByCustomerAsync(null, null, cancellationToken);

        Assert.Collection(result,
            first =>
            {
                Assert.Equal(wayne.Id, first.CustomerId);
                Assert.Equal(2, first.OrdersCount);
                Assert.Equal(3, first.TotalItems);
                Assert.Equal(125m, first.TotalAmount);
                Assert.Equal(62.5m, first.AverageTicket);
                Assert.Equal(100m, first.LargestOrderTotal);
                Assert.Equal(25m, first.SmallestOrderTotal);
                Assert.Equal(wayneFirstOrderAt, first.FirstOrderAt);
                Assert.Equal(wayneSecondOrderAt, first.LastOrderAt);
            },
            second =>
            {
                Assert.Equal(kent.Id, second.CustomerId);
                Assert.Equal(1, second.OrdersCount);
                Assert.Equal(3, second.TotalItems);
                Assert.Equal(90m, second.TotalAmount);
                Assert.Equal(90m, second.AverageTicket);
                Assert.Equal(90m, second.LargestOrderTotal);
                Assert.Equal(90m, second.SmallestOrderTotal);
                Assert.Equal(kentOrderAt, second.FirstOrderAt);
                Assert.Equal(kentOrderAt, second.LastOrderAt);
            });
    }

    [Fact]
    public async Task GetSalesByCustomerAsync_ShouldRespectDateRange()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ReportHandler(context);
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Selina Kyle",
            Email = "selina@antiquevault.com",
            Phone = "+55 11 95555-5555",
            BirthDate = new DateTime(1985, 7, 10, 0, 0, 0, DateTimeKind.Utc)
        };

        context.Customers.Add(customer);

        var olderOrderAt = new DateTime(2025, 1, 5, 10, 0, 0, DateTimeKind.Utc);
        var recentOrderAt = new DateTime(2025, 2, 20, 18, 30, 0, DateTimeKind.Utc);

        context.Orders.AddRange(
            CreateOrder(customer.Id, 1, 99m, olderOrderAt),
            CreateOrder(customer.Id, 2, 45m, recentOrderAt));

        await context.SaveChangesAsync(cancellationToken);

        var report = await handler.GetSalesByCustomerAsync(
            new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            cancellationToken);

        var summary = Assert.Single(report);
        Assert.Equal(customer.Id, summary.CustomerId);
        Assert.Equal(1, summary.OrdersCount);
        Assert.Equal(2, summary.TotalItems);
        Assert.Equal(90m, summary.TotalAmount);
        Assert.Equal(90m, summary.AverageTicket);
        Assert.Equal(recentOrderAt, summary.FirstOrderAt);
        Assert.Equal(recentOrderAt, summary.LastOrderAt);
    }

    [Fact]
    public async Task GetRevenueByPeriodAsync_ShouldGroupByMonth()
    {
        await using var context = InMemoryContextFactory.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        var customerId = Guid.NewGuid();

        context.Orders.AddRange(
            CreateOrder(customerId, 1, 100m, new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc)),
            CreateOrder(customerId, 2, 75m, new DateTime(2025, 1, 20, 14, 0, 0, DateTimeKind.Utc)),
            CreateOrder(customerId, 1, 120m, new DateTime(2025, 2, 3, 9, 0, 0, DateTimeKind.Utc)));

        await context.SaveChangesAsync(cancellationToken);

        var handler = new ReportHandler(context);

        var result = await handler.GetRevenueByPeriodAsync(
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            RevenuePeriod.Month,
            cancellationToken);

        Assert.Collection(result,
            january =>
            {
                Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), january.PeriodStart);
                Assert.Equal(new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc), january.PeriodEnd);
                Assert.Equal(250m, january.TotalAmount);
                Assert.Equal(2, january.OrdersCount);
                Assert.Equal(3, january.TotalItems);
                Assert.Equal(125m, january.AverageTicket);
                Assert.Equal(150m, january.LargestOrderTotal);
                Assert.Equal(100m, january.SmallestOrderTotal);
            },
            february =>
            {
                Assert.Equal(new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc), february.PeriodStart);
                Assert.Equal(new DateTime(2025, 2, 28, 23, 59, 59, DateTimeKind.Utc), february.PeriodEnd);
                Assert.Equal(120m, february.TotalAmount);
                Assert.Equal(1, february.OrdersCount);
                Assert.Equal(1, february.TotalItems);
                Assert.Equal(120m, february.AverageTicket);
                Assert.Equal(120m, february.LargestOrderTotal);
                Assert.Equal(120m, february.SmallestOrderTotal);
            });
    }

    private static Order CreateOrder(Guid customerId, int quantity, decimal totalPerItem, DateTime createdAt)
    {
        var orderId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var total = quantity * totalPerItem;

        return new Order
        {
            Id = orderId,
            CustomerId = customerId,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Lines =
            {
                new OrderLine
                {
                    Id = lineId,
                    OrderId = orderId,
                    ProductId = Guid.NewGuid(),
                    Quantity = quantity,
                    Total = total
                }
            }
        };
    }
}
