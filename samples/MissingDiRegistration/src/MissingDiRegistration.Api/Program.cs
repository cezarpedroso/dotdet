using MissingDiRegistration.Application.Notifications;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.AddScoped<NotificationCoordinator>();
builder.Services.AddSingleton<NotificationDispatcher>();
// ITemplateRenderer is intentionally not registered for DI002 calibration.

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapPost("/api/notifications", (SendNotificationRequest request, NotificationDispatcher dispatcher) =>
    Results.Accepted(value: dispatcher.Dispatch(request.Recipient, request.Message)));

app.Run();

public sealed record SendNotificationRequest(string Recipient, string Message);
