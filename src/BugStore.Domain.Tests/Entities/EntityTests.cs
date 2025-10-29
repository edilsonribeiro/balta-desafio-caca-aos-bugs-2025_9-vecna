using BugStore.Domain.Entities;

namespace BugStore.Domain.Tests.Entities;

public class CustomerTests
{
    [Fact]
    public void CreateCustomer_ShouldPersistState()
    {
        var id = Guid.NewGuid();
        var birthDate = new DateTime(1990, 5, 10, 8, 30, 0, DateTimeKind.Utc);

        var customer = new Customer
        {
            Id = id,
            Name = "Bruce Wayne",
            Email = "bruce@wayneenterprises.com",
            Phone = "+55 11 99999-9999",
            BirthDate = birthDate
        };

        Assert.Equal(id, customer.Id);
        Assert.Equal("Bruce Wayne", customer.Name);
        Assert.Equal("bruce@wayneenterprises.com", customer.Email);
        Assert.Equal("+55 11 99999-9999", customer.Phone);
        Assert.Equal(birthDate, customer.BirthDate);
    }
}

public class ProductTests
{
    [Fact]
    public void CreateProduct_ShouldPersistState()
    {
        var id = Guid.NewGuid();

        var product = new Product
        {
            Id = id,
            Title = "Batarang",
            Description = "Carbon fiber batarang for nightly patrols",
            Slug = "batarang-carbon-fiber",
            Price = 249.99m
        };

        Assert.Equal(id, product.Id);
        Assert.Equal("Batarang", product.Title);
        Assert.Equal("Carbon fiber batarang for nightly patrols", product.Description);
        Assert.Equal("batarang-carbon-fiber", product.Slug);
        Assert.Equal(249.99m, product.Price);
    }
}

public class OrderTests
{
    [Fact]
    public void NewOrder_ShouldStartWithEmptyLines()
    {
        var order = new Order();

        Assert.Empty(order.Lines);
    }

    [Fact]
    public void AddingLine_ShouldStoreLineData()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var line = new OrderLine
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            Total = 499.98m
        };

        order.Lines.Add(line);

        Assert.Single(order.Lines);
        Assert.Equal(line, order.Lines[0]);
    }
}

public class OrderLineTests
{
    [Fact]
    public void CreateOrderLine_ShouldPersistState()
    {
        var id = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var line = new OrderLine
        {
            Id = id,
            OrderId = orderId,
            ProductId = productId,
            Quantity = 3,
            Total = 749.97m
        };

        Assert.Equal(id, line.Id);
        Assert.Equal(orderId, line.OrderId);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(749.97m, line.Total);
    }
}
