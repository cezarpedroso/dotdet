namespace RiskyMinimalApi.Core.Inventory;

public interface IInventoryCatalog
{
    IReadOnlyList<InventoryItem> List();
}

public sealed class InventoryCatalog : IInventoryCatalog
{
    public IReadOnlyList<InventoryItem> List() =>
    [
        new("SKU-100", "Workshop keyboard", 12),
        new("SKU-200", "Docking station", 4)
    ];
}

public sealed record InventoryItem(string Sku, string Name, int AvailableQuantity);
