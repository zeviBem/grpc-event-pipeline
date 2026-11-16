using Grpc.Core;
using Notifications.Contracts.V1;

namespace Notifications.Client;

internal sealed class NotificationDemo
{
    private const string RecipientId = "user-42";
    private const int NotificationsToStream = 3;

    private readonly NotificationService.NotificationServiceClient _client;

    public NotificationDemo(NotificationService.NotificationServiceClient client)
    {
        _client = client;
    }

    public async Task RunAsync()
    {
        var lastSent = await StreamWhileSendingAsync();
        await ShowHistoryAsync();
        await ShowLookupByIdAsync(lastSent.Id);
        await ShowErrorStatusCodesAsync();
        await ShowDeadlineAsync();
    }

    private async Task<Notification> StreamWhileSendingAsync()
    {
        PrintSection("1. Live stream: subscribe, receive, cancel");

        using var cancellation = new CancellationTokenSource();
        using var call = _client.StreamNotifications(
            new StreamNotificationsRequest { RecipientId = RecipientId },
            cancellationToken: cancellation.Token);

        await call.ResponseHeadersAsync;
        Console.WriteLine($"Subscribed to notifications of '{RecipientId}'.");

        var receiving = ReceiveThenCancelAsync(call.ResponseStream, cancellation);

        var sent = new List<Notification>();
        for (var number = 1; number <= NotificationsToStream; number++)
        {
            sent.Add(await SendAsync($"Notification #{number}", $"Body of notification #{number}"));
        }

        await receiving;
        return sent[^1];
    }

    private static async Task ReceiveThenCancelAsync(
        IAsyncStreamReader<Notification> stream,
        CancellationTokenSource cancellation)
    {
        var received = 0;
        try
        {
            await foreach (var notification in stream.ReadAllAsync())
            {
                Console.WriteLine($"  <- received '{notification.Title}'");

                if (++received == NotificationsToStream)
                {
                    cancellation.Cancel();
                }
            }
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.Cancelled)
        {
            Console.WriteLine($"Subscription cancelled after {received} notifications.");
        }
    }

    private async Task<Notification> SendAsync(string title, string body)
    {
        var response = await _client.SendNotificationAsync(new SendNotificationRequest
        {
            RecipientId = RecipientId,
            Title = title,
            Body = body,
        });

        Console.WriteLine($"  -> sent '{response.Notification.Title}' (id {response.Notification.Id})");
        return response.Notification;
    }

    private async Task ShowHistoryAsync()
    {
        PrintSection("2. History (max_results = 2, newest first)");

        var response = await _client.GetNotificationHistoryAsync(new NotificationHistoryRequest
        {
            RecipientId = RecipientId,
            MaxResults = 2,
        });

        foreach (var notification in response.Notifications)
        {
            Console.WriteLine($"  {notification.CreatedAt.ToDateTimeOffset():HH:mm:ss.fff}  {notification.Title}");
        }
    }

    private async Task ShowLookupByIdAsync(string id)
    {
        PrintSection("3. Get notification by id");

        var notification = await _client.GetNotificationAsync(new GetNotificationRequest { Id = id });
        Console.WriteLine($"  Found '{notification.Title}' for '{notification.RecipientId}'");
    }

    private async Task ShowErrorStatusCodesAsync()
    {
        PrintSection("4. Error status codes");

        await PrintStatusAsync(
            "Send with empty fields",
            () => _client.SendNotificationAsync(new SendNotificationRequest { RecipientId = RecipientId }).ResponseAsync);

        await PrintStatusAsync(
            "Get with malformed id",
            () => _client.GetNotificationAsync(new GetNotificationRequest { Id = "not-a-uuid" }).ResponseAsync);

        await PrintStatusAsync(
            "Get with unknown id",
            () => _client.GetNotificationAsync(new GetNotificationRequest { Id = Guid.NewGuid().ToString() }).ResponseAsync);

        await PrintStatusAsync(
            "History with max_results = 0",
            () => _client.GetNotificationHistoryAsync(new NotificationHistoryRequest { RecipientId = RecipientId, MaxResults = 0 }).ResponseAsync);
    }

    private async Task ShowDeadlineAsync()
    {
        PrintSection("5. Deadline (stream with a 2 second deadline)");

        await PrintStatusAsync("Stream with deadline", async () =>
        {
            using var call = _client.StreamNotifications(
                new StreamNotificationsRequest { RecipientId = "nobody-sends-here" },
                deadline: DateTime.UtcNow.AddSeconds(2));

            await foreach (var _ in call.ResponseStream.ReadAllAsync())
            {
            }
        });
    }

    private static async Task PrintStatusAsync(string scenario, Func<Task> call)
    {
        try
        {
            await call();
            Console.WriteLine($"  {scenario}: OK");
        }
        catch (RpcException exception)
        {
            var detail = string.IsNullOrEmpty(exception.Status.Detail) ? "" : $" - {exception.Status.Detail}";
            Console.WriteLine($"  {scenario}: {exception.StatusCode}{detail}");
        }
    }

    private static void PrintSection(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {title} ===");
    }
}
