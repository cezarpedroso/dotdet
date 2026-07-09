using Forge.SampleShop.Domain;

namespace Forge.SampleShop.Application.Orders;

public interface IOrderWorkflow
{
    Task<Order> GetLatestOrderAsync(CancellationToken cancellationToken);
}
