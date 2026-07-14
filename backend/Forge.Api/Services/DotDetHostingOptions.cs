namespace Forge.Api.Services;

public sealed class DotDetHostingOptions
{
    public const string SectionName = "Hosting";

    public string[] AllowedOrigins { get; set; } = [];

    public string[] KnownProxies { get; set; } = [];
}
