using System.Collections.Concurrent;
using System.Threading.Channels;
using Notifications.Server.Application;
using Notifications.Server.Application.Abstractions;
using Notifications.Server.Application.Exceptions;
using Notifications.Server.Domain;

namespace Notifications.Server.Infrastructure;

public sealed class InMemoryNotificationBroadcaster : INotificationBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();

    public void Publish(Notification notification)
    {
        foreach (var (_, subscriber) in _subscribers)
        {
            if (subscriber.RecipientId == notification.RecipientId && !subscriber.Writer.TryWrite(notification))
            {
                CloseWithOverflow(subscriber);
            }
        }
    }

    public NotificationSubscription Subscribe(string recipientId)
    {
        var channel = Channel.CreateBounded<Notification>(new BoundedChannelOptions(NotificationLimits.SubscriberBufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

        var subscriptionId = Guid.NewGuid();
        _subscribers[subscriptionId] = new Subscriber(subscriptionId, recipientId, channel.Writer);

        return new NotificationSubscription(channel.Reader.ReadAllAsync, () => Unsubscribe(subscriptionId));
    }

    private void CloseWithOverflow(Subscriber subscriber)
    {
        if (_subscribers.TryRemove(subscriber.Id, out _))
        {
            subscriber.Writer.TryComplete(new SubscriberOverflowException(
                "The subscriber fell behind and its buffer is full. Reconnect to continue."));
        }
    }

    private void Unsubscribe(Guid subscriptionId)
    {
        if (_subscribers.TryRemove(subscriptionId, out var subscriber))
        {
            subscriber.Writer.TryComplete();
        }
    }

    private sealed record Subscriber(Guid Id, string RecipientId, ChannelWriter<Notification> Writer);
}
