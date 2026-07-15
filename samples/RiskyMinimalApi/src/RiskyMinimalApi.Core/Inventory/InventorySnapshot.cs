namespace RiskyMinimalApi.Core.Inventory;

public sealed class RequestInventoryScope
{
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
}

public sealed class InventorySnapshot
{
    private readonly RequestInventoryScope _requestScope;

    public InventorySnapshot(RequestInventoryScope requestScope)
    {
        _requestScope = requestScope;
    }

    public DateTimeOffset CapturedAt() => _requestScope.StartedAt;
}
