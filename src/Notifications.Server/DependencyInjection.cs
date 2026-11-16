using Notifications.Server.Application;
using Notifications.Server.Application.Abstractions;
using Notifications.Server.Infrastructure;

namespace Notifications.Server;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the notification feature. All services are singletons:
    /// the store and the broadcaster hold shared in-memory state, and the service is stateless.
    /// </summary>
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddSingleton<INotificationStore, InMemoryNotificationStore>();
        services.AddSingleton<INotificationBroadcaster, InMemoryNotificationBroadcaster>();
        services.AddSingleton<INotificationService, NotificationService>();

        return services;
    }
}
