using SemanticFixture.Api.Services;

namespace SemanticFixture.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IWidgetService, WidgetService>();
        return services;
    }
}
