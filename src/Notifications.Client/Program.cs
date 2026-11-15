using Grpc.Net.Client;
using Notifications.Client;
using Notifications.Contracts.V1;

var serverAddress = args.Length > 0 ? args[0] : "http://localhost:5050";

using var channel = GrpcChannel.ForAddress(serverAddress);
var client = new NotificationService.NotificationServiceClient(channel);

Console.WriteLine($"Connected to {serverAddress}");

await new NotificationDemo(client).RunAsync();
