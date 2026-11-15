namespace Notifications.Server.Domain;

public sealed record Notification(
    Guid Id,
    string RecipientId,
    string Title,
    string Body,
    DateTimeOffset CreatedAt
);
