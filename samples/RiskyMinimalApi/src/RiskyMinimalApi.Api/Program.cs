using RiskyMinimalApi.Core.Inventory;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IInventoryCatalog, InventoryCatalog>();
builder.Services.AddSingleton<IInventoryCatalog, InventoryCatalog>();
builder.Services.AddScoped<RequestInventoryScope>();
builder.Services.AddSingleton<InventorySnapshot>();
builder.Services.AddDbContext<InventoryDbContext>(options => options.UseInMemoryDatabase("RiskyInventory"));
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseAuthorization();

app.MapGet("/api/inventory", (IInventoryCatalog catalog) => Results.Ok(catalog.List()))
    .RequireAuthorization();
app.MapGet("/api/inventory/{sku}", (string sku, IInventoryCatalog catalog) =>
    catalog.List().FirstOrDefault(item => item.Sku == sku) is { } item
        ? Results.Ok(item)
        : Results.NotFound());

app.Run();
