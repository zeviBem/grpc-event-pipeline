using Notifications.Server.Application.Exceptions;

namespace Notifications.Server.Application;

internal static class NotificationValidator
{
    public const int MaxRecipientIdLength = 128;
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 4000;

    public static void ValidateSend(SendNotificationCommand command)
    {
        var errors = new List<string>();

        CheckRequiredText(command.RecipientId, "recipient_id", MaxRecipientIdLength, errors);
        CheckRequiredText(command.Title, "title", MaxTitleLength, errors);
        CheckRequiredText(command.Body, "body", MaxBodyLength, errors);

        ThrowIfAny(errors);
    }

    public static void ValidateRecipientId(string recipientId)
    {
        var errors = new List<string>();

        CheckRequiredText(recipientId, "recipient_id", MaxRecipientIdLength, errors);

        ThrowIfAny(errors);
    }

    public static void ValidateHistoryRequest(string recipientId, int? maxResults, int maxAllowed)
    {
        var errors = new List<string>();

        CheckRequiredText(recipientId, "recipient_id", MaxRecipientIdLength, errors);

        if (maxResults is < 1 || maxResults > maxAllowed)
        {
            errors.Add($"max_results must be between 1 and {maxAllowed}.");
        }

        ThrowIfAny(errors);
    }

    public static Guid ParseNotificationId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ValidationException("id is required.");
        }

        if (!Guid.TryParse(id, out var notificationId))
        {
            throw new ValidationException("id must be a valid UUID.");
        }

        return notificationId;
    }

    private static void CheckRequiredText(string value, string fieldName, int maxLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
        else if (value.Length > maxLength)
        {
            errors.Add($"{fieldName} must be at most {maxLength} characters.");
        }
    }

    private static void ThrowIfAny(List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
