using System.Text.Json.Serialization;
using Forge.Api.Analysis;
using Forge.Api.Analyzers;
using Forge.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("DotDetDashboard", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "https://localhost:5173",
                "http://127.0.0.1:5173",
                "https://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

builder.Services.AddScoped<SolutionAnalysisService>();
builder.Services.AddSingleton<SemanticAnalysisHelper>();
builder.Services.AddSingleton<IssueEnrichmentService>();
builder.Services.AddSingleton<RuleCatalogService>();
builder.Services.AddSingleton<ArchitectureMapService>();
builder.Services.AddSingleton<EngineeringAssessmentService>();
builder.Services.AddSingleton<SuppressionService>();
builder.Services.AddScoped<ArchitectureAnalyzer>();
builder.Services.AddScoped<DependencyInjectionAnalyzer>();
builder.Services.AddScoped<EfCoreAnalyzer>();
builder.Services.AddScoped<SecurityConfigurationAnalyzer>();
builder.Services.AddScoped<ApiReadinessAnalyzer>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<ZipExtractionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.UseCors("DotDetDashboard");
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
