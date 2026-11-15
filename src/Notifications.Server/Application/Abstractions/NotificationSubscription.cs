using System.Threading.Channels;
using Notifications.Server.Domain;

namespace Notifications.Server.Application.Abstractions;

public sealed class NotificationSubscription : IDisposable
{
    private readonly ChannelReader<Notification> _reader;
    private readonly Action _unsubscribe;

    public NotificationSubscription(ChannelReader<Notification> reader, Action unsubscribe)
    {
        _reader = reader;
        _unsubscribe = unsubscribe;
    }

    public IAsyncEnumerable<Notification> ReadAllAsync(CancellationToken cancellationToken) =>
        _reader.ReadAllAsync(cancellationToken);

    public void Dispose() => _unsubscribe();
}
