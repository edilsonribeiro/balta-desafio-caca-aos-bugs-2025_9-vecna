using System;
using BugStore.Domain.Entities;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using BugStore.Application.Responses.Common;
using CreateCustomerRequest = BugStore.Application.Requests.Customers.Create;
using UpdateCustomerRequest = BugStore.Application.Requests.Customers.Update;
using CreateCustomerResponse = BugStore.Application.Responses.Customers.Create;
using GetCustomerByIdResponse = BugStore.Application.Responses.Customers.GetById;
using GetCustomersResponse = BugStore.Application.Responses.Customers.Get;
using UpdateCustomerResponse = BugStore.Application.Responses.Customers.Update;

namespace BugStore.Application.Handlers.Customers;

public class CustomerHandler
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _context;

    public CustomerHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<GetCustomersResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Name)
            .Select(customer => new GetCustomersResponse(
                customer.Id,
                customer.Name,
                customer.Email,
                customer.Phone,
                customer.BirthDate))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<GetCustomersResponse>> SearchAsync(
        string? term,
        int page = 1,
        int pageSize = DefaultPageSize,
        string? sortBy = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize);

        var query = _context.Customers
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{EscapeLikePattern(term.Trim())}%";
            query = query.Where(customer =>
                EF.Functions.Like(customer.Name, pattern, "\\") ||
                EF.Functions.Like(customer.Email, pattern, "\\") ||
                EF.Functions.Like(customer.Phone, pattern, "\\"));
        }

        query = ApplySorting(query, sortBy, sortOrder);

        var total = await query.CountAsync(cancellationToken);

        var customers = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(customer => new GetCustomersResponse(
                customer.Id,
                customer.Name,
                customer.Email,
                customer.Phone,
                customer.BirthDate))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetCustomersResponse>(customers, total, normalizedPage, normalizedPageSize);
    }

    public async Task<GetCustomerByIdResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

        return customer is null
            ? null
            : new GetCustomerByIdResponse(
                customer.Id,
                customer.Name,
                customer.Email,
                customer.Phone,
                customer.BirthDate);
    }

    public async Task<CreateCustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            BirthDate = request.BirthDate
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateCustomerResponse(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.BirthDate);
    }

    public async Task<UpdateCustomerResponse?> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (customer is null)
            return null;

        customer.Name = request.Name;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.BirthDate = request.BirthDate;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateCustomerResponse(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.BirthDate);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (customer is null)
            return false;

        _context.Customers.Remove(customer);
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

    private static IQueryable<Customer> ApplySorting(IQueryable<Customer> query, string? sortBy, string? sortOrder)
    {
        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.ToLowerInvariant()) switch
        {
            "email" => descending
                ? query.OrderByDescending(customer => customer.Email).ThenBy(customer => customer.Name)
                : query.OrderBy(customer => customer.Email).ThenBy(customer => customer.Name),
            "birthdate" => descending
                ? query.OrderByDescending(customer => customer.BirthDate).ThenBy(customer => customer.Name)
                : query.OrderBy(customer => customer.BirthDate).ThenBy(customer => customer.Name),
            "name" or null or "" => descending
                ? query.OrderByDescending(customer => customer.Name)
                : query.OrderBy(customer => customer.Name),
            _ => query
                .OrderBy(customer => customer.Name)
        };
    }
}
