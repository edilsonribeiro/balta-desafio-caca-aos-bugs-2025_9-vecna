using BugStore.Application.Contracts.Customers;
using BugStore.Application.Handlers.Customers;
using BugStore.Application.Services.Customers;
using BugStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CustomerHandler>());
builder.Services.AddScoped<ICustomerAppService, CustomerAppService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapOpenApi();
app.MapScalarApiReference();
app.MapControllers();

app.Run();

public partial class Program;
