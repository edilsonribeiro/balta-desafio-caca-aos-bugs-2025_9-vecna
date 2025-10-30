using AutoMapper;
using BugStore.Application.Caching;
using BugStore.Domain.Entities;
using BugStore.Domain.Repositories;
using MediatR;
using CreateCustomerResponse = BugStore.Application.Responses.Customers.Create;
using UpdateCustomerResponse = BugStore.Application.Responses.Customers.Update;

namespace BugStore.Application.Handlers.Customers.Commands;

public class CustomerCommandHandler :
    IRequestHandler<CreateCustomerCommand, CreateCustomerResponse>,
    IRequestHandler<UpdateCustomerCommand, UpdateCustomerResponse?>,
    IRequestHandler<DeleteCustomerCommand, bool>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICustomerCacheSignal _cacheSignal;

    public CustomerCommandHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork, IMapper mapper, ICustomerCacheSignal cacheSignal)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _cacheSignal = cacheSignal;
    }

    public async Task<CreateCustomerResponse> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = _mapper.Map<Customer>(request.Request);
        customer.Id = Guid.NewGuid();

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _cacheSignal.SignalChange();

        return _mapper.Map<CreateCustomerResponse>(customer);
    }

    public async Task<UpdateCustomerResponse?> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (customer is null)
            return null;

        _mapper.Map(request.Request, customer);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _cacheSignal.SignalChange();

        return _mapper.Map<UpdateCustomerResponse>(customer);
    }

    public async Task<bool> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (customer is null)
            return false;

        _customerRepository.Remove(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _cacheSignal.SignalChange();
        return true;
    }
}
