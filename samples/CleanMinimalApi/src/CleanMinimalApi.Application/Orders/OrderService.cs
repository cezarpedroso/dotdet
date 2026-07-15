using CleanMinimalApi.Domain.Orders;
using Microsoft.Extensions.Logging;

namespace CleanMinimalApi.Application.Orders;

public interface IOrderService
{
    IReadOnlyList<Order> List();

    Order Create(string customerName);
}

public sealed class OrderService(ILogger<OrderService> logger) : IOrderService
{
    private readonly List<Order> _orders = [];

    public IReadOnlyList<Order> List() => _orders;

    public Order Create(string customerName)
    {
        var order = new Order(Guid.NewGuid(), customerName, DateTimeOffset.UtcNow);
        _orders.Add(order);
        logger.LogInformation("Created order {OrderId}", order.Id);
        return order;
    }
}
