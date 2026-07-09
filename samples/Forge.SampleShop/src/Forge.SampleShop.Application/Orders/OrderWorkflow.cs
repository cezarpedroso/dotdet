using Forge.SampleShop.Domain;
using Forge.SampleShop.Infrastructure.Persistence;

namespace Forge.SampleShop.Application.Orders;

// Intentional smell for DotDet: Application depends on Infrastructure directly.
public sealed class OrderWorkflow : IOrderWorkflow
{
    private readonly SampleShopDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;

    public OrderWorkflow(SampleShopDbContext dbContext, IPaymentGateway paymentGateway)
    {
        _dbContext = dbContext;
        _paymentGateway = paymentGateway;
    }

    public Task<Order> GetLatestOrderAsync(CancellationToken cancellationToken)
    {
        _paymentGateway.Authorize("demo-order");

        return Task.FromResult(new Order
        {
            OrderId = Guid.NewGuid(),
            CustomerEmail = "customer@example.com"
        });
    }
}
