using Google.Protobuf.WellKnownTypes;
using Notifications.Server.Application;
using Notifications.Server.Domain;
using Proto = Notifications.Contracts.V1;

namespace Notifications.Server.Api;

internal static class NotificationMapper
{
    public static Proto.Notification ToContract(this Notification notification) => new()
    {
        Id = notification.Id.ToString(),
        RecipientId = notification.RecipientId,
        Title = notification.Title,
        Body = notification.Body,
        CreatedAt = Timestamp.FromDateTimeOffset(notification.CreatedAt),
    };

    public static SendNotificationCommand ToCommand(this Proto.SendNotificationRequest request) =>
        new(request.RecipientId, request.Title, request.Body);
}
