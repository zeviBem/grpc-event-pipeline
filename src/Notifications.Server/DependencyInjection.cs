using Notifications.Server.Application;
using Notifications.Server.Application.Abstractions;
using Notifications.Server.Infrastructure;

namespace Notifications.Server;

public static class DependencyInjection
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddSingleton<INotificationStore, InMemoryNotificationStore>();
        services.AddSingleton<INotificationBroadcaster, InMemoryNotificationBroadcaster>();
        services.AddSingleton<INotificationService, NotificationService>();

        return services;
    }
}
