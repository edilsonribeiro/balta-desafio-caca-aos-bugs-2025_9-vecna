using AutoMapper;
using BugStore.Domain.Entities;
using BugStore.Application.Queries.Customers.Models;
using CreateCustomerRequest = BugStore.Application.Requests.Customers.Create;
using UpdateCustomerRequest = BugStore.Application.Requests.Customers.Update;
using CreateCustomerResponse = BugStore.Application.Responses.Customers.Create;
using UpdateCustomerResponse = BugStore.Application.Responses.Customers.Update;

namespace BugStore.Application.Mapping.Profiles;

public class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        CreateMap<CreateCustomerRequest, Customer>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());

        CreateMap<UpdateCustomerRequest, Customer>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());

        CreateMap<Customer, CustomerListItem>()
            .ConstructUsing(customer => new CustomerListItem(customer.Id, customer.Name, customer.Email, customer.Phone, customer.BirthDate));

        CreateMap<Customer, CustomerDetails>()
            .ConstructUsing(customer => new CustomerDetails(customer.Id, customer.Name, customer.Email, customer.Phone, customer.BirthDate));

        CreateMap<Customer, CreateCustomerResponse>()
            .ConstructUsing(customer => new CreateCustomerResponse(customer.Id, customer.Name, customer.Email, customer.Phone, customer.BirthDate));

        CreateMap<Customer, UpdateCustomerResponse>()
            .ConstructUsing(customer => new UpdateCustomerResponse(customer.Id, customer.Name, customer.Email, customer.Phone, customer.BirthDate));
    }
}
