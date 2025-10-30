using System;
using BugStore.Application.Responses.Common;
using BugStore.Domain.Entities;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using CreateProductRequest = BugStore.Application.Requests.Products.Create;
using UpdateProductRequest = BugStore.Application.Requests.Products.Update;
using CreateProductResponse = BugStore.Application.Responses.Products.Create;
using GetProductByIdResponse = BugStore.Application.Responses.Products.GetById;
using GetProductsResponse = BugStore.Application.Responses.Products.Get;
using UpdateProductResponse = BugStore.Application.Responses.Products.Update;

namespace BugStore.Application.Handlers.Products;

public class ProductHandler(AppDbContext context)
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _context = context;

    public async Task<IReadOnlyList<GetProductsResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .OrderBy(product => product.Title)
            .Select(product => new GetProductsResponse(
                product.Id,
                product.Title,
                product.Description,
                product.Slug,
                product.Price))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<GetProductsResponse>> SearchAsync(
        string? term,
        int page = 1,
        int pageSize = DefaultPageSize,
        string? sortBy = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize);

        var query = _context.Products
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{EscapeLikePattern(term.Trim())}%";
            query = query.Where(product =>
                EF.Functions.Like(product.Title, pattern, "\\") ||
                EF.Functions.Like(product.Description, pattern, "\\") ||
                EF.Functions.Like(product.Slug, pattern, "\\"));
        }

        query = ApplySorting(query, sortBy, sortOrder);

        var total = await query.CountAsync(cancellationToken);

        var products = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(product => new GetProductsResponse(
                product.Id,
                product.Title,
                product.Description,
                product.Slug,
                product.Price))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetProductsResponse>(products, total, normalizedPage, normalizedPageSize);
    }

    public async Task<GetProductByIdResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        return product is null
            ? null
            : new GetProductByIdResponse(
                product.Id,
                product.Title,
                product.Description,
                product.Slug,
                product.Price);
    }

    public async Task<CreateProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Slug = request.Slug,
            Price = request.Price
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateProductResponse(
            product.Id,
            product.Title,
            product.Description,
            product.Slug,
            product.Price);
    }

    public async Task<UpdateProductResponse?> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (product is null)
            return null;

        product.Title = request.Title;
        product.Description = request.Description;
        product.Slug = request.Slug;
        product.Price = request.Price;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateProductResponse(
            product.Id,
            product.Title,
            product.Description,
            product.Slug,
            product.Price);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (product is null)
            return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
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

    private static IQueryable<Product> ApplySorting(IQueryable<Product> query, string? sortBy, string? sortOrder)
    {
        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.ToLowerInvariant()) switch
        {
            "price" => descending
                ? query.OrderByDescending(product => (double)product.Price).ThenBy(product => product.Title)
                : query.OrderBy(product => (double)product.Price).ThenBy(product => product.Title),
            "slug" => descending
                ? query.OrderByDescending(product => product.Slug).ThenBy(product => product.Title)
                : query.OrderBy(product => product.Slug).ThenBy(product => product.Title),
            "title" or null or "" => descending
                ? query.OrderByDescending(product => product.Title)
                : query.OrderBy(product => product.Title),
            _ => query.OrderBy(product => product.Title)
        };
    }
}
