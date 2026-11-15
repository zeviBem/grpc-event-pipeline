# grpc-event-pipeline

A gRPC Notification Service in .NET 8: send notifications, read history, and stream live notifications.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Optional: [grpcurl](https://github.com/fullstorydev/grpcurl) for manual testing

## How to run the server

```bash
dotnet run --project src/Notifications.Server
```

The server listens on `http://localhost:5050` (HTTP/2, no TLS). gRPC reflection is enabled in Development, so grpcurl works without the `.proto` file.

## How to test

### Demo client

With the server running, in a second terminal:

```bash
dotnet run --project src/Notifications.Client
```

The client subscribes to a live stream, sends 3 notifications and receives them, cancels the subscription, reads history, looks up a notification by id, and shows the error status codes and a deadline.

### grpcurl

The examples below use bash syntax (Git Bash on Windows works).

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

## Error codes

| Situation | Status code |
|---|---|
| Missing or invalid field, malformed id, `max_results` out of range | `InvalidArgument` |
| `GetNotification` with an unknown id | `NotFound` |
| Client cancelled the call | `Cancelled` |
| Deadline passed | `DeadlineExceeded` |
| Unexpected server error (details are logged, not returned) | `Internal` |

## Key design decisions

- **Layers.** `Api` (gRPC endpoints, mapping) → `Application` (use cases, validation) → `Infrastructure` (in-memory store and broadcaster). The application layer does not know about gRPC, and storage sits behind interfaces, so it can be replaced by a database without touching the other layers.
- **Shared contract project.** `Notifications.Contracts` holds the `.proto` and generates both server and client code, so they cannot drift apart.
- **One request message per RPC.** History and streaming are separate RPCs with separate requests, so each can evolve on its own. The package is versioned (`notifications.v1`).
- **Required vs optional.** proto3 has no `required`. Required fields are validated on the server. `max_results` is `optional`, so "not sent" (use the default) is different from `0` (invalid).
- **Streaming with `Channel<T>`.** Each subscriber has its own bounded channel. Sending never waits for slow subscribers, waiting for new items does not block a thread, and a subscriber that stops reading cannot use unlimited memory (the oldest pending item is dropped).
- **Subscription handshake.** The server sends response headers once the subscription is active, so a client that waits for them does not miss notifications sent right after subscribing.
- **Centralized error handling.** An interceptor maps application exceptions to gRPC status codes. Endpoints have no try/catch, and raw exceptions never reach the client.
- **Scope.** Data is kept in memory and lost on restart. Authentication, database and message brokers are out of scope by design.
