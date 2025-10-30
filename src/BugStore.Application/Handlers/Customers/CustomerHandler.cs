using AutoMapper;
using AutoMapper.QueryableExtensions;
using BugStore.Domain.Entities;
using BugStore.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using BugStore.Application.Responses.Common;
using CreateCustomerRequest = BugStore.Application.Requests.Customers.Create;
using UpdateCustomerRequest = BugStore.Application.Requests.Customers.Update;
using CreateCustomerResponse = BugStore.Application.Responses.Customers.Create;
using GetCustomerByIdResponse = BugStore.Application.Responses.Customers.GetById;
using GetCustomersResponse = BugStore.Application.Responses.Customers.Get;
using UpdateCustomerResponse = BugStore.Application.Responses.Customers.Update;
using MediatR;

namespace BugStore.Application.Handlers.Customers;

public record SearchCustomersQuery(
    string? Term,
    int Page = 1,
    int PageSize = 25,
    string? SortBy = null,
    string? SortOrder = null) : IRequest<PagedResult<GetCustomersResponse>>;

public record GetCustomerByIdQuery(Guid Id) : IRequest<GetCustomerByIdResponse?>;

public record CreateCustomerCommand(CreateCustomerRequest Request) : IRequest<CreateCustomerResponse>;

public record UpdateCustomerCommand(Guid Id, UpdateCustomerRequest Request) : IRequest<UpdateCustomerResponse?>;

public record DeleteCustomerCommand(Guid Id) : IRequest<bool>;

public class CustomerHandler :
    IRequestHandler<SearchCustomersQuery, PagedResult<GetCustomersResponse>>,
    IRequestHandler<GetCustomerByIdQuery, GetCustomerByIdResponse?>,
    IRequestHandler<CreateCustomerCommand, CreateCustomerResponse>,
    IRequestHandler<UpdateCustomerCommand, UpdateCustomerResponse?>,
    IRequestHandler<DeleteCustomerCommand, bool>
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CustomerHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public Task<PagedResult<GetCustomersResponse>> Handle(SearchCustomersQuery request, CancellationToken cancellationToken) =>
        SearchAsync(request.Term, request.Page, request.PageSize, request.SortBy, request.SortOrder, cancellationToken);

    public Task<GetCustomerByIdResponse?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken) =>
        GetByIdAsync(request.Id, cancellationToken);

    public Task<CreateCustomerResponse> Handle(CreateCustomerCommand request, CancellationToken cancellationToken) =>
        CreateAsync(request.Request, cancellationToken);

    public Task<UpdateCustomerResponse?> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken) =>
        UpdateAsync(request.Id, request.Request, cancellationToken);

    public Task<bool> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken) =>
        DeleteAsync(request.Id, cancellationToken);

    public async Task<IReadOnlyList<GetCustomersResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _customerRepository
            .Query()
            .AsNoTracking()
            .OrderBy(customer => customer.Name)
            .ProjectTo<GetCustomersResponse>(_mapper.ConfigurationProvider)
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

        var query = _customerRepository
            .Query()
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
            .ProjectTo<GetCustomersResponse>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        return new PagedResult<GetCustomersResponse>(customers, total, normalizedPage, normalizedPageSize);
    }

    public async Task<GetCustomerByIdResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

        return _mapper.Map<GetCustomerByIdResponse?>(customer);
    }

    public async Task<CreateCustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = _mapper.Map<Customer>(request);
        customer.Id = Guid.NewGuid();

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<CreateCustomerResponse>(customer);
    }

    public async Task<UpdateCustomerResponse?> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer is null)
            return null;

        _mapper.Map(request, customer);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<UpdateCustomerResponse>(customer);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer is null)
            return false;

        _customerRepository.Remove(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
