using Notifications.Server.Domain;

namespace Notifications.Server.Application.Abstractions;

/// <summary>
/// Stores notifications. The in-memory implementation can be replaced by a database
/// without changing the application or gRPC layers.
/// </summary>
public interface INotificationStore
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken);

    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns the recipient's notifications, most recent first.</summary>
    Task<IReadOnlyList<Notification>> GetByRecipientAsync(
        string recipientId,
        int maxResults,
        CancellationToken cancellationToken);
}
