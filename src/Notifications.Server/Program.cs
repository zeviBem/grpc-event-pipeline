using Notifications.Server;
using Notifications.Server.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc(options => options.Interceptors.Add<ExceptionMappingInterceptor>());
builder.Services.AddGrpcReflection();
builder.Services.AddNotifications();

var app = builder.Build();

app.MapGrpcService<NotificationGrpcService>();

if (app.Environment.IsDevelopment())
{
    // Lets tools like grpcurl discover the services without the .proto file.
    app.MapGrpcReflectionService();
}

app.Run();
