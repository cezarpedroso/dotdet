using Forge.SampleShop.Infrastructure.Persistence;

namespace Forge.SampleShop.Application.Orders;

// Intentional smell for DotDet: registered as singleton while capturing a scoped DbContext.
public sealed class OrderExportJob
{
    private readonly SampleShopDbContext _dbContext;

    public OrderExportJob(SampleShopDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public int CountTrackedOrders()
    {
        return _dbContext.ChangeTracker.Entries().Count();
    }
}
