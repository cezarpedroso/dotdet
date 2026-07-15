using RiskyMinimalApi.Persistence;

namespace RiskyMinimalApi.Application;

public sealed class InventoryWorkflow(InventoryProjection projection)
{
    public int CountTrackedItems() => projection.Query().Count();
}
