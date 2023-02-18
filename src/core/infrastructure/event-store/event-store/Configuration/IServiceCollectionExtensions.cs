using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Core.Infrastructure.Configuration;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configure EventStore services
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="settings">The <see cref="EventStoreClientSettings"/> to use</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddEventStore(this IServiceCollection services, EventStoreClientSettings settings)
    {
        services.TryAddSingleton(new EventStoreClient(settings));
        services.TryAddSingleton(new EventStorePersistentSubscriptionsClient(settings));
        services.TryAddSingleton(new EventStoreProjectionManagementClient(settings));
        return services;
    }

}
