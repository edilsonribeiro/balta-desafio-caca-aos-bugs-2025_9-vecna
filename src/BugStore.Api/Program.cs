
using BugStore.Application.Handlers.Customers;
using BugStore.Application.Handlers.Orders;
using BugStore.Application.Handlers.Products;
using BugStore.Application.Handlers.Reports;
using BugStore.Application.Responses.Reports;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using CustomerCreateRequest = BugStore.Application.Requests.Customers.Create;
using CustomerUpdateRequest = BugStore.Application.Requests.Customers.Update;
using OrderCreateRequest = BugStore.Application.Requests.Orders.Create;
using ProductCreateRequest = BugStore.Application.Requests.Products.Create;
using ProductUpdateRequest = BugStore.Application.Requests.Products.Update;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));
builder.Services.AddScoped<CustomerHandler>();
builder.Services.AddScoped<ProductHandler>();
builder.Services.AddScoped<OrderHandler>();
builder.Services.AddScoped<ReportHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/", () => "Hello World!");

app.MapGet("/v1/customers", async ([AsParameters] ListQuery query, CustomerHandler handler, CancellationToken cancellationToken) =>
{
    var customers = await handler.SearchAsync(query.Term, query.Page, query.PageSize, query.SortBy, query.SortOrder, cancellationToken);
    return Results.Ok(customers);
});

app.MapGet("/v1/customers/{id:guid}", async (Guid id, CustomerHandler handler, CancellationToken cancellationToken) =>
{
    var customer = await handler.GetByIdAsync(id, cancellationToken);
    return customer is null
        ? Results.NotFound()
        : Results.Ok(customer);
});

app.MapPost("/v1/customers", async (CustomerCreateRequest request, CustomerHandler handler, CancellationToken cancellationToken) =>
{
    var customer = await handler.CreateAsync(request, cancellationToken);
    return Results.Created($"/v1/customers/{customer.Id}", customer);
});

app.MapPut("/v1/customers/{id:guid}", async (Guid id, CustomerUpdateRequest request, CustomerHandler handler, CancellationToken cancellationToken) =>
{
    var customer = await handler.UpdateAsync(id, request, cancellationToken);
    return customer is null
        ? Results.NotFound()
        : Results.Ok(customer);
});

app.MapDelete("/v1/customers/{id:guid}", async (Guid id, CustomerHandler handler, CancellationToken cancellationToken) =>
{
    var deleted = await handler.DeleteAsync(id, cancellationToken);
    return deleted
        ? Results.NoContent()
        : Results.NotFound();
});

app.MapGet("/v1/products", async ([AsParameters] ListQuery query, ProductHandler handler, CancellationToken cancellationToken) =>
{
    var products = await handler.SearchAsync(query.Term, query.Page, query.PageSize, query.SortBy, query.SortOrder, cancellationToken);
    return Results.Ok(products);
});

app.MapGet("/v1/products/{id:guid}", async (Guid id, ProductHandler handler, CancellationToken cancellationToken) =>
{
    var product = await handler.GetByIdAsync(id, cancellationToken);
    return product is null
        ? Results.NotFound()
        : Results.Ok(product);
});

app.MapPost("/v1/products", async (ProductCreateRequest request, ProductHandler handler, CancellationToken cancellationToken) =>
{
    var product = await handler.CreateAsync(request, cancellationToken);
    return Results.Created($"/v1/products/{product.Id}", product);
});

app.MapPut("/v1/products/{id:guid}", async (Guid id, ProductUpdateRequest request, ProductHandler handler, CancellationToken cancellationToken) =>
{
    var product = await handler.UpdateAsync(id, request, cancellationToken);
    return product is null
        ? Results.NotFound()
        : Results.Ok(product);
});

app.MapDelete("/v1/products/{id:guid}", async (Guid id, ProductHandler handler, CancellationToken cancellationToken) =>
{
    var result = await handler.DeleteAsync(id, cancellationToken);
    return result switch
    {
        DeleteProductResult.Deleted => Results.NoContent(),
        DeleteProductResult.NotFound => Results.NotFound(),
        DeleteProductResult.InUse => Results.Conflict("Não é possível remover um produto associado a pedidos.")
    };
});

app.MapGet("/v1/orders", async ([AsParameters] ListQuery query, OrderHandler handler, CancellationToken cancellationToken) =>
{
    var orders = await handler.SearchAsync(query.Term, query.Page, query.PageSize, query.SortBy, query.SortOrder, cancellationToken);
    return Results.Ok(orders);
});

app.MapGet("/v1/orders/{id:guid}", async (Guid id, OrderHandler handler, CancellationToken cancellationToken) =>
{
    var order = await handler.GetByIdAsync(id, cancellationToken);
    return order is null
        ? Results.NotFound()
        : Results.Ok(order);
});
app.MapPost("/v1/orders", async (OrderCreateRequest request, OrderHandler handler, CancellationToken cancellationToken) =>
{
    var order = await handler.CreateAsync(request, cancellationToken);
    return order is null
        ? Results.BadRequest()
        : Results.Created($"/v1/orders/{order.Id}", order);
});

app.MapGet("/v1/reports/sales-by-customer", async ([AsParameters] ReportQuery query, ReportHandler handler, CancellationToken cancellationToken) =>
{
    if (query.StartDate.HasValue && query.EndDate.HasValue && query.StartDate > query.EndDate)
        return Results.BadRequest("O parâmetro startDate não pode ser maior que endDate.");

    var result = await handler.GetSalesByCustomerAsync(query.StartDate, query.EndDate, cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/v1/reports/revenue-by-period", async ([AsParameters] ReportQuery query, ReportHandler handler, CancellationToken cancellationToken) =>
{
    if (query.StartDate.HasValue && query.EndDate.HasValue && query.StartDate > query.EndDate)
        return Results.BadRequest("O parâmetro startDate não pode ser maior que endDate.");

    var (isValid, period) = TryParsePeriod(query.GroupBy);
    if (!isValid)
        return Results.BadRequest("Valor inválido para groupBy. Utilize day, month ou year.");

    var result = await handler.GetRevenueByPeriodAsync(query.StartDate, query.EndDate, period, cancellationToken);
    return Results.Ok(result);
});

static (bool IsValid, RevenuePeriod Period) TryParsePeriod(string? groupBy) =>
    (groupBy ?? "day").ToLowerInvariant() switch
    {
        "day" or "" => (true, RevenuePeriod.Day),
        "month" => (true, RevenuePeriod.Month),
        "year" => (true, RevenuePeriod.Year),
        _ => (false, RevenuePeriod.Day)
    };

app.Run();

public partial class Program;

public record ListQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 25,
    string? SortBy = null,
    string? SortOrder = null);

public record ReportQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? GroupBy = "day");
