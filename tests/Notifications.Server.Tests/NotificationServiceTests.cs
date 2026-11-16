using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Server.Application;
using Notifications.Server.Application.Abstractions;
using Notifications.Server.Application.Exceptions;
using Notifications.Server.Domain;
using Notifications.Server.Infrastructure;

namespace Notifications.Server.Tests;

// These tests use the real in-memory store and broadcaster, so no mocking library is needed.
public class NotificationServiceTests
{
    private readonly NotificationService _service = new(
        new InMemoryNotificationStore(),
        new InMemoryNotificationBroadcaster(),
        NullLogger<NotificationService>.Instance);

    private static SendNotificationCommand Command(string recipient = "user-1", string title = "Hello") =>
        new(recipient, title, "Body text");

    [Fact]
    public async Task Send_StoresNotification_SoItCanBeFetchedById()
    {
        var sent = await _service.SendAsync(Command(), CancellationToken.None);

        var fetched = await _service.GetAsync(sent.Id.ToString(), CancellationToken.None);

        Assert.Equal(sent, fetched);
    }

    [Fact]
    public async Task Send_WithEmptyTitle_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SendAsync(Command(title: ""), CancellationToken.None));
    }

    [Fact]
    public async Task Get_WithUnknownId_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetAsync(Guid.NewGuid().ToString(), CancellationToken.None));
    }

    [Fact]
    public async Task History_ReturnsNewestFirst_AndRespectsMaxResults()
    {
        await _service.SendAsync(Command(title: "first"), CancellationToken.None);
        await _service.SendAsync(Command(title: "second"), CancellationToken.None);
        await _service.SendAsync(Command(title: "third"), CancellationToken.None);

        var history = await _service.GetHistoryAsync("user-1", maxResults: 2, CancellationToken.None);

        Assert.Equal(["third", "second"], history.Select(n => n.Title));
    }

    [Fact]
    public async Task Subscribe_ReceivesOnlyNotificationsOfItsRecipient()
    {
        using var subscription = _service.Subscribe("user-1");

        await _service.SendAsync(Command(recipient: "someone-else"), CancellationToken.None);
        var sent = await _service.SendAsync(Command(recipient: "user-1"), CancellationToken.None);

        // Timeout so a broken test fails instead of hanging forever.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var received in subscription.ReadAllAsync(timeout.Token))
        {
            Assert.Equal(sent.Id, received.Id);
            break;
        }
    }

    [Fact]
    public async Task Sends_AreDeliveredInStoredOrder_EvenWhenTheFirstStoreCallIsSlow()
    {
        // The fake store makes the first send slow: it stores the notification, then waits.
        // Without the send lock the second send would finish and publish first,
        // so the stream order would differ from the stored order. The test forces that timing.
        var store = new SlowFirstStore();
        var service = new NotificationService(
            store,
            new InMemoryNotificationBroadcaster(),
            NullLogger<NotificationService>.Instance);
        using var subscription = service.Subscribe("user-1");

        var first = Task.Run(() => service.SendAsync(Command(title: "first"), CancellationToken.None));
        await store.FirstIsStored.WaitAsync(TimeSpan.FromSeconds(5));

        var second = Task.Run(() => service.SendAsync(Command(title: "second"), CancellationToken.None));

        // With the lock, the second send waits here for the first. Without it, it finishes at once.
        await Task.WhenAny(second, Task.Delay(200));
        store.ReleaseFirst();
        await Task.WhenAll(first, second);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var streamed = new List<string>();
        await foreach (var notification in subscription.ReadAllAsync(timeout.Token))
        {
            streamed.Add(notification.Title);
            if (streamed.Count == 2)
            {
                break;
            }
        }

        Assert.Equal(["first", "second"], streamed);
    }

    [Fact]
    public async Task Dispose_RemovesSubscription_SoItGetsNothingAndEnds()
    {
        var subscription = _service.Subscribe("user-1");
        subscription.Dispose();

        await _service.SendAsync(Command(recipient: "user-1"), CancellationToken.None);

        // The stream is closed, so reading ends immediately with no items.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<Notification>();
        await foreach (var notification in subscription.ReadAllAsync(timeout.Token))
        {
            received.Add(notification);
        }

        Assert.Empty(received);
    }

    [Fact]
    public async Task SlowSubscriber_IsClosedWithOverflowError_AndSenderIsNotBlocked()
    {
        using var subscription = _service.Subscribe("user-1");

        // The subscriber never reads, so its buffer fills up. One extra send overflows it.
        for (var i = 0; i <= NotificationLimits.SubscriberBufferSize; i++)
        {
            await _service.SendAsync(Command(), CancellationToken.None);
        }

        // The subscriber still gets everything that was buffered, and then the error.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = 0;

        await Assert.ThrowsAsync<SubscriberOverflowException>(async () =>
        {
            await foreach (var _ in subscription.ReadAllAsync(timeout.Token))
            {
                received++;
            }
        });

        Assert.Equal(NotificationLimits.SubscriberBufferSize, received);
    }
}

/// <summary>
/// Wraps the real store, but the first AddAsync stores the notification and then waits
/// until the test releases it. This lets a test control the timing of concurrent sends.
/// </summary>
file sealed class SlowFirstStore : INotificationStore
{
    private readonly InMemoryNotificationStore _inner = new();
    private readonly TaskCompletionSource _firstIsStored = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;

    public Task FirstIsStored => _firstIsStored.Task;

    public void ReleaseFirst() => _releaseFirst.SetResult();

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken)
    {
        await _inner.AddAsync(notification, cancellationToken);

        if (Interlocked.Increment(ref _calls) == 1)
        {
            _firstIsStored.SetResult();
            await _releaseFirst.Task;
        }
    }

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _inner.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Notification>> GetByRecipientAsync(
        string recipientId,
        int maxResults,
        CancellationToken cancellationToken) =>
        _inner.GetByRecipientAsync(recipientId, maxResults, cancellationToken);
}
