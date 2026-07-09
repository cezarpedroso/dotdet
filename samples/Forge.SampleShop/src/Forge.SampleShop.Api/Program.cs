using Forge.SampleShop.Application.Orders;
using Forge.SampleShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddDbContext<SampleShopDbContext>(options =>
    options.UseInMemoryDatabase("SampleShop"));
builder.Services.AddScoped<IOrderWorkflow, OrderWorkflow>();
builder.Services.AddScoped<IOrderWorkflow, OrderWorkflow>();
builder.Services.AddSingleton<OrderExportJob>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthorization();

app.MapControllers();

app.Run();
