using System;
using BugStore.Application.Responses.Common;
using BugStore.Domain.Entities;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using CreateOrderRequest = BugStore.Application.Requests.Orders.Create;
using CreateOrderResponse = BugStore.Application.Responses.Orders.Create;
using GetOrderByIdResponse = BugStore.Application.Responses.Orders.GetById;
using OrderLineResponse = BugStore.Application.Responses.Orders.Line;
using SearchOrderResponse = BugStore.Application.Responses.Orders.Search;

namespace BugStore.Application.Handlers.Orders;

public class OrderHandler(AppDbContext context)
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public async Task<GetOrderByIdResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await context.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .ThenInclude(line => line.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
            return null;

        return MapOrder(order);
    }

    public async Task<PagedResult<SearchOrderResponse>> SearchAsync(
        string? term,
        int page = 1,
        int pageSize = DefaultPageSize,
        string? sortBy = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize);

        var query = context.Orders
            .AsNoTracking()
            .Include(order => order.Customer)
            .Include(order => order.Lines)
                .ThenInclude(line => line.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmedTerm = term.Trim();
            var pattern = $"%{EscapeLikePattern(trimmedTerm)}%";
            var hasGuidFilter = Guid.TryParse(trimmedTerm, out var guidFilter);

            query = query.Where(order =>
                (hasGuidFilter && (order.Id == guidFilter || order.CustomerId == guidFilter)) ||
                (order.Customer != null && (
                    EF.Functions.Like(order.Customer.Name, pattern, "\\") ||
                    EF.Functions.Like(order.Customer.Email, pattern, "\\") ||
                    EF.Functions.Like(order.Customer.Phone, pattern, "\\"))) ||
                order.Lines.Any(line =>
                    line.Product != null && (
                        EF.Functions.Like(line.Product.Title, pattern, "\\") ||
                        EF.Functions.Like(line.Product.Description, pattern, "\\") ||
                        EF.Functions.Like(line.Product.Slug, pattern, "\\"))));
        }

        query = ApplySorting(query, sortBy, sortOrder);

        var total = await query.CountAsync(cancellationToken);

        var orders = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(order => new SearchOrderResponse(
                order.Id,
                order.CustomerId,
                order.Customer != null ? order.Customer.Name : string.Empty,
                order.Lines.Sum(line => line.Total),
                order.CreatedAt,
                order.UpdatedAt,
                order.Lines
                    .Select(line => new OrderLineResponse(
                        line.Id,
                        line.ProductId,
                        line.Product != null ? line.Product.Title : string.Empty,
                        line.Quantity,
                        line.Total))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<SearchOrderResponse>(orders, total, normalizedPage, normalizedPageSize);
    }

    public async Task<CreateOrderResponse?> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Lines is null || request.Lines.Count == 0 || request.Lines.Any(line => line.Quantity <= 0))
            return null;

        var customerExists = await context.Customers
            .AsNoTracking()
            .AnyAsync(customer => customer.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
            return null;

        var productIds = request.Lines
            .Select(line => line.ProductId)
            .Distinct()
            .ToList();

        var products = await context.Products
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        if (products.Count != productIds.Count)
            return null;

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var line in request.Lines)
        {
            var product = products[line.ProductId];
            var lineTotal = product.Price * line.Quantity;

            order.Lines.Add(new OrderLine
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = line.Quantity,
                Total = lineTotal
            });
        }

        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);

        var responseLines = order.Lines
            .Select(line => new OrderLineResponse(
                line.Id,
                line.ProductId,
                products[line.ProductId].Title,
                line.Quantity,
                line.Total))
            .ToList();

        var total = responseLines.Sum(line => line.Total);

        return new CreateOrderResponse(
            order.Id,
            order.CustomerId,
            total,
            order.CreatedAt,
            order.UpdatedAt,
            responseLines);
    }

    private static GetOrderByIdResponse MapOrder(Order order)
    {
        var responseLines = order.Lines
            .Select(line => new OrderLineResponse(
                line.Id,
                line.ProductId,
                line.Product?.Title ?? string.Empty,
                line.Quantity,
                line.Total))
            .ToList();

        var total = responseLines.Sum(line => line.Total);

        return new GetOrderByIdResponse(
            order.Id,
            order.CustomerId,
            total,
            order.CreatedAt,
            order.UpdatedAt,
            responseLines);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }

    private static (int Page, int PageSize) NormalizePagination(int page, int pageSize)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return (normalizedPage, normalizedPageSize);
    }

    private static IQueryable<Order> ApplySorting(IQueryable<Order> query, string? sortBy, string? sortOrder)
    {
        var descending = sortOrder is null || string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.ToLowerInvariant()) switch
        {
            "updatedat" => descending
                ? query.OrderByDescending(order => order.UpdatedAt).ThenByDescending(order => order.CreatedAt)
                : query.OrderBy(order => order.UpdatedAt).ThenBy(order => order.CreatedAt),
            "total" => descending
                ? query.OrderByDescending(order => order.Lines.Sum(line => (double)line.Total)).ThenByDescending(order => order.CreatedAt)
                : query.OrderBy(order => order.Lines.Sum(line => (double)line.Total)).ThenBy(order => order.CreatedAt),
            "createdat" or null or "" => descending
                ? query.OrderByDescending(order => order.CreatedAt)
                : query.OrderBy(order => order.CreatedAt),
            _ => query.OrderByDescending(order => order.CreatedAt)
        };
    }
}
