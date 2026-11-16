# grpc-event-pipeline

A gRPC Notification Service in .NET 8: send a notification, stream live notifications, and read notification history.

## How to run the server

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet run --project src/Notifications.Server
```

The server listens on `http://localhost:5050` (HTTP/2, no TLS). gRPC reflection is enabled in Development, so grpcurl works without the `.proto` file. Data is kept in memory and is lost on restart.

## How to test

### Option 1: demo client

With the server running, in a second terminal:

```bash
dotnet run --project src/Notifications.Client
```

The client subscribes to a live stream, sends 3 notifications and receives them, cancels the subscription, reads history, looks up a notification by id, and shows the error status codes and a deadline.

### Option 2: grpcurl

Requires [grpcurl](https://github.com/fullstorydev/grpcurl). The examples use bash syntax (Git Bash on Windows works).

List services:

```bash
grpcurl -plaintext localhost:5050 list
```

Subscribe to live notifications (keep it running, press Ctrl+C to cancel):

```bash
grpcurl -plaintext -d '{"recipient_id": "user-42"}' localhost:5050 notifications.v1.NotificationService/StreamNotifications
```

Send a notification (in another terminal):

```bash
grpcurl -plaintext -d '{"recipient_id": "user-42", "title": "Build finished", "body": "Build #128 passed"}' localhost:5050 notifications.v1.NotificationService/SendNotification
```

Get history:

```bash
grpcurl -plaintext -d '{"recipient_id": "user-42", "max_results": 10}' localhost:5050 notifications.v1.NotificationService/GetNotificationHistory
```

Get one notification by id:

```bash
grpcurl -plaintext -d '{"id": "<id from SendNotification>"}' localhost:5050 notifications.v1.NotificationService/GetNotification
```

### Automated tests

A few unit tests cover the core service logic (send, get, history order, validation, live subscription):

```bash
dotnet test
```

## Example request / response

`SendNotification` request:

```json
{ "recipient_id": "user-42", "title": "Build finished", "body": "Build #128 passed" }
```

Response:

```json
{
  "notification": {
    "id": "5265c84f-6795-45a9-9477-b9996e29ec2d",
    "recipientId": "user-42",
    "title": "Build finished",
    "body": "Build #128 passed",
    "createdAt": "2026-11-15T09:39:56.866Z"
  }
}
```

Invalid request (empty title and body):

```
ERROR:
  Code: InvalidArgument
  Message: title is required. body is required.
```

## Key design decisions

- **Layers.** `Api` → `Application` → `Infrastructure`. Storage and broadcasting sit behind interfaces, so they can be replaced without touching the other layers.
- **Contract.** One request message per RPC, and `max_results` is `optional` so "not sent" (use the default) differs from `0` (invalid).
- **Streaming.** Each subscriber has its own bounded `Channel<T>`. A subscriber that falls too far behind is closed with `ResourceExhausted` instead of silently losing notifications.
- **Subscription handshake.** The server sends response headers once the subscription is active, so a client that waits for them does not miss notifications.
- **Errors.** One interceptor maps exceptions to gRPC status codes, so endpoints have no try/catch and raw exceptions never reach the client.

## Known limitations

Out of scope here (no database, auth or brokers):

- Data is in memory only, with no size limit, and the broadcaster works for a single server instance.
- No authentication, and no limit on subscriptions per recipient.
- No resume: a reconnecting client misses what was sent meanwhile. A `sequence` number would fix it.
- No idempotency: retrying `SendNotification` creates a duplicate.
- A client that stops reading keeps its stream handler blocked until it disconnects. A write timeout would fix it.
