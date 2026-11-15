using Notifications.Server.Application.Abstractions;
using Notifications.Server.Domain;

namespace Notifications.Server.Application;

/// <summary>
/// The use cases of the notification service. Knows nothing about gRPC.
/// Throws <see cref="Exceptions.ValidationException"/> for invalid input and
/// <see cref="Exceptions.NotFoundException"/> for unknown ids.
/// </summary>
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
