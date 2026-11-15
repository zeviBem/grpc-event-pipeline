namespace Notifications.Server.Application.Exceptions;

public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public ValidationException(string error)
        : this([error])
    {}

    public IReadOnlyList<string> Errors { get; }
}
