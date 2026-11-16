namespace Notifications.Server.Application.Exceptions;

public sealed class SubscriberOverflowException : Exception
{
    public SubscriberOverflowException(string message) : base(message) {}
}
