namespace MissingDiRegistration.Domain.Notifications;

public sealed record Notification(Guid Id, string Recipient, string Subject, string Body);
