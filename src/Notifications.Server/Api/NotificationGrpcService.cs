using Grpc.Core;
using Notifications.Server.Application;
using Proto = Notifications.Contracts.V1;

namespace Notifications.Server.Api;

public sealed class NotificationGrpcService : Proto.NotificationService.NotificationServiceBase
{
    private readonly INotificationService _notifications;

    public NotificationGrpcService(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public override async Task<Proto.SendNotificationResponse> SendNotification(
        Proto.SendNotificationRequest request,
        ServerCallContext context)
    {
        var notification = await _notifications.SendAsync(request.ToCommand(), context.CancellationToken);

        return new Proto.SendNotificationResponse { Notification = notification.ToContract() };
    }

    public override async Task<Proto.Notification> GetNotification(
        Proto.GetNotificationRequest request,
        ServerCallContext context)
    {
        var notification = await _notifications.GetAsync(request.Id, context.CancellationToken);

        return notification.ToContract();
    }

    public override async Task<Proto.NotificationHistoryResponse> GetNotificationHistory(
        Proto.NotificationHistoryRequest request,
        ServerCallContext context)
    {
        int? maxResults = request.HasMaxResults ? request.MaxResults : null;

        var history = await _notifications.GetHistoryAsync(
            request.RecipientId,
            maxResults,
            context.CancellationToken);

        var response = new Proto.NotificationHistoryResponse();
        response.Notifications.AddRange(history.Select(notification => notification.ToContract()));
        return response;
    }

    public override async Task StreamNotifications(
        Proto.StreamNotificationsRequest request,
        IServerStreamWriter<Proto.Notification> responseStream,
        ServerCallContext context)
    {
        using var subscription = _notifications.Subscribe(request.RecipientId);

        await context.WriteResponseHeadersAsync(new Metadata());

        await foreach (var notification in subscription.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(notification.ToContract(), context.CancellationToken);
        }
    }
}
