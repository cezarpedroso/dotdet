using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissingDiRegistration.Domain.Notifications;

namespace MissingDiRegistration.Application.Notifications;

public sealed class NotificationOptions
{
    public string SenderName { get; init; } = "DotDet Sample";
}

public interface ITemplateRenderer
{
    string Render(string recipient, string message);
}

public sealed class NotificationCoordinator
{
    private readonly ITemplateRenderer _templates;
    private readonly ILogger<NotificationCoordinator> _logger;
    private readonly IConfiguration _configuration;
    private readonly IOptions<NotificationOptions> _options;

    public NotificationCoordinator(
        ITemplateRenderer templates,
        ILogger<NotificationCoordinator> logger,
        IConfiguration configuration,
        IOptions<NotificationOptions> options)
    {
        _templates = templates;
        _logger = logger;
        _configuration = configuration;
        _options = options;
    }

    public Notification Create(string recipient, string message)
    {
        _logger.LogInformation("Preparing notification using channel {Channel}", _configuration["Notifications:Channel"]);
        return new Notification(
            Guid.NewGuid(),
            recipient,
            $"Message from {_options.Value.SenderName}",
            _templates.Render(recipient, message));
    }
}

public sealed class NotificationDispatcher
{
    private readonly NotificationCoordinator _coordinator;

    public NotificationDispatcher(NotificationCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public Notification Dispatch(string recipient, string message) => _coordinator.Create(recipient, message);
}
