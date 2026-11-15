using Notifications.Server.Domain;

namespace Notifications.Server.Application.Abstractions;

public interface INotificationBroadcaster
{
    void Publish(Notification notification);

    NotificationSubscription Subscribe(string recipientId);
}
