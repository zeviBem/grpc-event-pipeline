using Notifications.Server.Domain;

namespace Notifications.Server.Application.Abstractions;

public sealed class NotificationSubscription : IDisposable
{
    private readonly Func<CancellationToken, IAsyncEnumerable<Notification>> _read;
    private readonly Action _unsubscribe;

    public NotificationSubscription(
        Func<CancellationToken, IAsyncEnumerable<Notification>> read,
        Action unsubscribe)
    {
        _read = read;
        _unsubscribe = unsubscribe;
    }

    public IAsyncEnumerable<Notification> ReadAllAsync(CancellationToken cancellationToken) =>
        _read(cancellationToken);

    public void Dispose() => _unsubscribe();
}
