namespace Forge.Api.Services;

public sealed class AnalysisExecutionOptions
{
    public const string SectionName = "AnalysisExecution";

    public int PermitLimit { get; set; } = 6;

    public int WindowSeconds { get; set; } = 60;

    public int MaxConcurrentPerCaller { get; set; } = 1;

    public int TimeoutSeconds { get; set; } = 300;
}
