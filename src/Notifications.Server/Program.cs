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
    app.MapGrpcReflectionService();
}

app.Run();
