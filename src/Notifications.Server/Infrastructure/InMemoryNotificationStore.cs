using Notifications.Server.Application.Abstractions;
using Notifications.Server.Domain;

namespace Notifications.Server.Infrastructure;

public sealed class InMemoryNotificationStore : INotificationStore
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, Notification> _byId = new();
    private readonly Dictionary<string, List<Notification>> _byRecipient = new();

    public Task AddAsync(Notification notification, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _byId.Add(notification.Id, notification);

            if (!_byRecipient.TryGetValue(notification.RecipientId, out var recipientNotifications))
            {
                recipientNotifications = new List<Notification>();
                _byRecipient.Add(notification.RecipientId, recipientNotifications);
            }

            recipientNotifications.Add(notification);
        }

        return Task.CompletedTask;
    }

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            return Task.FromResult(_byId.GetValueOrDefault(id));
        }
    }

    public Task<IReadOnlyList<Notification>> GetByRecipientAsync(
        string recipientId,
        int maxResults,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (!_byRecipient.TryGetValue(recipientId, out var recipientNotifications))
            {
                return Task.FromResult<IReadOnlyList<Notification>>(Array.Empty<Notification>());
            }

            // Notifications are appended in the order they were sent, so the newest are at the end.
            // A copy is returned, so callers never see the list change under them.
            IReadOnlyList<Notification> newestFirst = recipientNotifications
                .AsEnumerable()
                .Reverse()
                .Take(maxResults)
                .ToList();

            return Task.FromResult(newestFirst);
        }
    }
}
