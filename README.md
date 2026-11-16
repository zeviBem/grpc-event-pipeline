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

- **Layers.** `Api` (gRPC endpoints) → `Application` (use cases, validation) → `Infrastructure` (in-memory store and broadcaster). Storage sits behind interfaces, so it can be replaced without touching the other layers.
- **Contract.** `Notifications.Contracts` holds the `.proto` and generates both server and client code. Each RPC has its own request message, and `max_results` is `optional` so "not sent" (use the default) differs from `0` (invalid).
- **Streaming.** Each subscriber has its own bounded `Channel<T>`: sending never waits for slow subscribers, waiting does not block a thread, and memory per subscriber is bounded. A subscriber that falls too far behind is closed with `ResourceExhausted` instead of silently losing notifications, so the client knows to reconnect. Sends are processed one at a time, so history order and stream order always match.
- **Subscription handshake.** The server sends response headers once the subscription is active, so a client that waits for them does not miss notifications sent right after subscribing.
- **Errors.** One interceptor maps exceptions to gRPC status codes (`InvalidArgument`, `NotFound`, `ResourceExhausted`, `Cancelled`, `DeadlineExceeded`, `Internal`). Endpoints have no try/catch, and raw exceptions never reach the client.

## Known limitations

Deliberately out of scope for this assignment (no database, auth or brokers). What I would address next:

- **In-memory only.** Data is lost on restart and the store has no size limit or retention.
- **Single instance.** The broadcaster lives in the process memory. Several servers would need an external pub/sub.
- **No authentication.** Any client can subscribe to any `recipient_id`.
- **No resume.** A slow subscriber is closed with `ResourceExhausted`, but a reconnecting client still misses what was sent meanwhile, and so does a client that reads history and then opens the stream. A `sequence` number plus a `resume_after_sequence` field would close both gaps.
- **Sends are serialized.** One lock keeps history and stream in the same order. This is acceptable at this scale (not measured), but a real database would use a sequence instead.
- **Publish scans all subscribers.** Each send checks every subscriber to find the matching recipient. An index by `recipient_id` would fix this if the number of subscribers grows.
- **Stuck clients.** A client that stops reading keeps its handler blocked on the write until it disconnects or the deadline expires. A write timeout would fix it.
- **No subscriber limit.** There is no cap on subscriptions per recipient.
- **No idempotency.** Retrying `SendNotification` creates a duplicate. An idempotency key in the request would prevent it.
- **Coarse validation errors.** All violations are returned in one message string. A per-field error model (`google.rpc.BadRequest`) would be machine-readable.
- **No keepalive.** A quiet stream may be closed by a proxy or load balancer. HTTP/2 keepalive pings or a heartbeat would prevent it.
- **Tests.** Only core service logic is covered. There are no tests for the interceptor, no end-to-end tests and no concurrency tests.
