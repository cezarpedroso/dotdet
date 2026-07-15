using RiskyMinimalApi.Core.Inventory;

namespace RiskyMinimalApi.Persistence;

public sealed class InventoryProjection(InventoryDbContext database)
{
    public IQueryable<InventoryItem> Query() => database.Inventory;
}
