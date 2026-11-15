using Microsoft.Extensions.Options;
using Notifications.Server.Application.Abstractions;
using Notifications.Server.Application.Exceptions;
using Notifications.Server.Domain;

namespace Notifications.Server.Application;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationStore _store;
    private readonly INotificationBroadcaster _broadcaster;
    private readonly TimeProvider _clock;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationStore store,
        INotificationBroadcaster broadcaster,
        TimeProvider clock,
        IOptions<NotificationOptions> options,
        ILogger<NotificationService> logger)
    {
        _store = store;
        _broadcaster = broadcaster;
        _clock = clock;
        _options = options.Value;
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
            CreatedAt: _clock.GetUtcNow());

        // Cancellation is honored up to here. Once stored, the notification is also
        // published, so history and live subscribers never disagree.
        await _store.AddAsync(notification, cancellationToken);
        _broadcaster.Publish(notification);

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
        NotificationValidator.ValidateHistoryRequest(recipientId, maxResults, _options.MaxHistorySize);

        return _store.GetByRecipientAsync(
            recipientId,
            maxResults ?? _options.DefaultHistorySize,
            cancellationToken);
    }

    public NotificationSubscription Subscribe(string recipientId)
    {
        NotificationValidator.ValidateRecipientId(recipientId);

        return _broadcaster.Subscribe(recipientId);
    }
}
