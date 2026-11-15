namespace Notifications.Server.Application;

public sealed record SendNotificationCommand(string RecipientId, string Title, string Body);
