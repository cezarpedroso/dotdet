namespace SemanticFixture.Api.Services;

public sealed class WidgetService : IWidgetService
{
    public string GetStatus()
    {
        return "ready";
    }
}
