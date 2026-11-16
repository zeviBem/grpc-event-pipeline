using Notifications.Server.Domain;

namespace Notifications.Server.Application.Abstractions;

public interface INotificationStore
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken);

    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Notification>> GetByRecipientAsync(
        string recipientId,
        int maxResults,
        CancellationToken cancellationToken);
}
