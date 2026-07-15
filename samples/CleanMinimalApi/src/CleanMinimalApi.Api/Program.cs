using CleanMinimalApi.Application.Orders;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IOrderService, OrderService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapOpenApi();
app.MapHealthChecks("/health");

app.MapGet("/api/orders", (IOrderService orders) => Results.Ok(orders.List()));
app.MapPost("/api/orders", (CreateOrderRequest request, IOrderService orders) =>
{
    if (string.IsNullOrWhiteSpace(request.CustomerName))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.CustomerName)] = ["Customer name is required."]
        });
    }

    return Results.Created("/api/orders", orders.Create(request.CustomerName));
});

app.Run();

public sealed record CreateOrderRequest([Required, MinLength(2)] string CustomerName);
