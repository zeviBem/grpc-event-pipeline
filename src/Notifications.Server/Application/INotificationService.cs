using Notifications.Server.Application.Abstractions;
using Notifications.Server.Domain;

namespace Notifications.Server.Application;

public interface INotificationService
{
    Task<Notification> SendAsync(SendNotificationCommand command, CancellationToken cancellationToken);

    Task<Notification> GetAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Notification>> GetHistoryAsync(
        string recipientId,
        int? maxResults,
        CancellationToken cancellationToken);

    NotificationSubscription Subscribe(string recipientId);
}
