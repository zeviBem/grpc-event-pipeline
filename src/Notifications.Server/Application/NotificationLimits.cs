namespace Notifications.Server.Application;

/// <summary>
/// Fixed limits of the notification service.
/// </summary>
public static class NotificationLimits
{
    /// <summary>History size used when the client does not send max_results.</summary>
    public const int DefaultHistorySize = 20;

    /// <summary>Largest max_results a client may ask for.</summary>
    public const int MaxHistorySize = 100;

    /// <summary>
    /// How many undelivered notifications are kept per live subscriber.
    /// When a slow subscriber falls behind and the buffer is full, its subscription is closed with an error.
    /// </summary>
    public const int SubscriberBufferSize = 100;
}
