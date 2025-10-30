using AutoMapper;
using AutoMapper.QueryableExtensions;
using BugStore.Application.Queries.Customers.Models;
using BugStore.Application.Responses.Common;
using BugStore.Domain.Entities;
using BugStore.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MediatR;

namespace BugStore.Application.Handlers.Customers.Queries;

public class CustomerQueryHandler(ICustomerRepository customerRepository, IMapper mapper, IMemoryCache cache) :
    IRequestHandler<SearchCustomersQuery, PagedResult<CustomerListItem>>,
    IRequestHandler<GetCustomerByIdQuery, CustomerDetails?>
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly ICustomerRepository _customerRepository = customerRepository;
    private readonly IMapper _mapper = mapper;
    private readonly IMemoryCache _cache = cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public async Task<PagedResult<CustomerListItem>> Handle(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        var (page, pageSize) = NormalizePagination(request.Page, request.PageSize);

        var cacheKey = BuildSearchCacheKey(request.Term, page, pageSize, request.SortBy, request.SortOrder);
        if (_cache.TryGetValue(cacheKey, out PagedResult<CustomerListItem>? cachedResult) && cachedResult is not null)
        {
            return cachedResult;
        }

        var query = _customerRepository
            .Query()
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Term))
        {
            var pattern = $"%{EscapeLikePattern(request.Term.Trim())}%";
            query = query.Where(customer =>
                EF.Functions.Like(customer.Name, pattern, "\\") ||
                EF.Functions.Like(customer.Email, pattern, "\\") ||
                EF.Functions.Like(customer.Phone, pattern, "\\"));
        }

        query = ApplySorting(query, request.SortBy, request.SortOrder);

        var total = await query.CountAsync(cancellationToken);

        var customers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<CustomerListItem>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<CustomerListItem>(customers, total, page, pageSize);
        _cache.Set(cacheKey, result, CacheDuration);

        return result;
    }

    public async Task<CustomerDetails?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = BuildByIdCacheKey(request.Id);
        if (_cache.TryGetValue(cacheKey, out CustomerDetails? cached) && cached is not null)
        {
            return cached;
        }

        var customer = await _customerRepository
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == request.Id, cancellationToken);

        var details = _mapper.Map<CustomerDetails?>(customer);
        if (details is not null)
        {
            _cache.Set(cacheKey, details, CacheDuration);
        }

        return details;
    }

    private static string EscapeLikePattern(string value) =>
        value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

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
            _ => query.OrderBy(customer => customer.Name)
        };
    }

    private static string BuildSearchCacheKey(string? term, int page, int pageSize, string? sortBy, string? sortOrder) =>
        $"customers:search:{term}:{page}:{pageSize}:{sortBy}:{sortOrder}";

    private static string BuildByIdCacheKey(Guid id) => $"customers:details:{id}";
}
