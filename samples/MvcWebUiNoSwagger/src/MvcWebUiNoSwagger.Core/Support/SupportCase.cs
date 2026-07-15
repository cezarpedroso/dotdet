namespace MvcWebUiNoSwagger.Core.Support;

public sealed record SupportCase(int Id, string Customer, string Status, DateTimeOffset UpdatedAt);
