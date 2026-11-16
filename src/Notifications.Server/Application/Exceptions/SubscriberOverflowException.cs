namespace Notifications.Server.Application.Exceptions;

/// <summary>
/// A live subscriber fell too far behind and its buffer filled up.
/// The subscription is closed, so the client knows it missed notifications and can reconnect.
/// </summary>
public sealed class SubscriberOverflowException : Exception
{
    public SubscriberOverflowException(string message) : base(message) {}
}
