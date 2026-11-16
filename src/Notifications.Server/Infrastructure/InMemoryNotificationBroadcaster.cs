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
        // Enumerating the dictionary itself does not lock it. '.Values' would lock it
        // and copy every subscriber on each publish.
        foreach (var (_, subscriber) in _subscribers)
        {
            if (subscriber.RecipientId == notification.RecipientId && !subscriber.Writer.TryWrite(notification))
            {
                // The buffer is full (or the subscription is already closed). Instead of silently
                // losing notifications, close this subscription with an error the client can see.
                // The reader still gets what is already buffered, then the error.
                CloseWithOverflow(subscriber);
            }
        }
    }

    public NotificationSubscription Subscribe(string recipientId)
    {
        var channel = Channel.CreateBounded<Notification>(new BoundedChannelOptions(NotificationLimits.SubscriberBufferSize)
        {
            // With Wait, TryWrite returns false when the buffer is full. It never blocks the sender.
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
