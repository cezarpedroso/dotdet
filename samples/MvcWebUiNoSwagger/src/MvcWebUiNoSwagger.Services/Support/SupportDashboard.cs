using MvcWebUiNoSwagger.Core.Support;

namespace MvcWebUiNoSwagger.Services.Support;

public interface ISupportDashboard
{
    IReadOnlyList<SupportCase> GetRecentCases();
}

public sealed class SupportDashboard : ISupportDashboard
{
    public IReadOnlyList<SupportCase> GetRecentCases() =>
    [
        new(1042, "Contoso", "Investigating", DateTimeOffset.UtcNow.AddMinutes(-18)),
        new(1041, "Fabrikam", "Resolved", DateTimeOffset.UtcNow.AddHours(-2))
    ];
}
