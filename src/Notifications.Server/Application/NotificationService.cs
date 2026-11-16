using Notifications.Server.Application.Abstractions;
using Notifications.Server.Application.Exceptions;
using Notifications.Server.Domain;

namespace Notifications.Server.Application;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationStore _store;
    private readonly INotificationBroadcaster _broadcaster;
    private readonly ILogger<NotificationService> _logger;
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    public NotificationService(
        INotificationStore store,
        INotificationBroadcaster broadcaster,
        ILogger<NotificationService> logger)
    {
        _store = store;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<Notification> SendAsync(SendNotificationCommand command, CancellationToken cancellationToken)
    {
        NotificationValidator.ValidateSend(command);

        var notification = new Notification(
            Id: Guid.NewGuid(),
            RecipientId: command.RecipientId,
            Title: command.Title,
            Body: command.Body,
            CreatedAt: DateTimeOffset.UtcNow);

        // Store and publish run one send at a time, so two concurrent sends cannot be stored
        // in one order and delivered in the other. Cancellation is honored until the store call:
        // once stored, the notification is also published, so history and live subscribers agree.
        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            await _store.AddAsync(notification, cancellationToken);
            _broadcaster.Publish(notification);
        }
        finally
        {
            _sendGate.Release();
        }

        _logger.LogInformation(
            "Notification {NotificationId} sent to {RecipientId}",
            notification.Id,
            notification.RecipientId);

        return notification;
    }

    public async Task<Notification> GetAsync(string id, CancellationToken cancellationToken)
    {
        var notificationId = NotificationValidator.ParseNotificationId(id);

        return await _store.GetByIdAsync(notificationId, cancellationToken)
            ?? throw new NotFoundException($"Notification '{notificationId}' was not found.");
    }

    public Task<IReadOnlyList<Notification>> GetHistoryAsync(
        string recipientId,
        int? maxResults,
        CancellationToken cancellationToken)
    {
        NotificationValidator.ValidateHistoryRequest(recipientId, maxResults);

        return _store.GetByRecipientAsync(
            recipientId,
            maxResults ?? NotificationLimits.DefaultHistorySize,
            cancellationToken);
    }

    public NotificationSubscription Subscribe(string recipientId)
    {
        NotificationValidator.ValidateRecipientId(recipientId);

        return _broadcaster.Subscribe(recipientId);
    }
}
