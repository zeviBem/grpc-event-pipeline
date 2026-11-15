using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Notifications.Server.Application;
using Notifications.Server.Application.Abstractions;
using Notifications.Server.Domain;

namespace Notifications.Server.Infrastructure;

public sealed class InMemoryNotificationBroadcaster : INotificationBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();
    private readonly int _subscriberBufferSize;

    public InMemoryNotificationBroadcaster(IOptions<NotificationOptions> options)
    {
        _subscriberBufferSize = options.Value.SubscriberBufferSize;
    }

    public void Publish(Notification notification)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            if (subscriber.RecipientId == notification.RecipientId)
            {
                subscriber.Writer.TryWrite(notification);
            }
        }
    }

    public NotificationSubscription Subscribe(string recipientId)
    {
        var channel = Channel.CreateBounded<Notification>(new BoundedChannelOptions(_subscriberBufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var subscriptionId = Guid.NewGuid();
        _subscribers[subscriptionId] = new Subscriber(recipientId, channel.Writer);

        return new NotificationSubscription(channel.Reader, () => Unsubscribe(subscriptionId));
    }

    private void Unsubscribe(Guid subscriptionId)
    {
        if (_subscribers.TryRemove(subscriptionId, out var subscriber))
        {
            subscriber.Writer.TryComplete();
        }
    }

    private sealed record Subscriber(string RecipientId, ChannelWriter<Notification> Writer);
}
