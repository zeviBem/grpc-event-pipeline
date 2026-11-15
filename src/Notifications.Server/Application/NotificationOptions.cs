using System.ComponentModel.DataAnnotations;

namespace Notifications.Server.Application;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>History size used when the client does not send max_results.</summary>
    [Range(1, 1000)]
    public int DefaultHistorySize { get; set; } = 20;

    /// <summary>Largest max_results a client may ask for.</summary>
    [Range(1, 1000)]
    public int MaxHistorySize { get; set; } = 100;

    /// <summary>
    /// How many undelivered notifications are kept per live subscriber.
    /// When a slow subscriber falls behind, the oldest ones are dropped.
    /// </summary>
    [Range(1, 10_000)]
    public int SubscriberBufferSize { get; set; } = 100;
}
