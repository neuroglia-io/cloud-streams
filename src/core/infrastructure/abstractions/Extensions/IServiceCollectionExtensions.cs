using CloudStreams.Core.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Core.Infrastructure.Configuration;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configures a new <see cref="IResourceController{TResource}"/> to control <see cref="IResource"/>s of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to control</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddResourceController<TResource>(this IServiceCollection services)
        where TResource : class, IResource, new()
    {
        services.TryAddSingleton<ResourceController<TResource>>();
        services.TryAddSingleton<IResourceController<TResource>>(provider => provider.GetRequiredService<ResourceController<TResource>>());
        services.AddHostedService(provider => provider.GetRequiredService<ResourceController<TResource>>());
        return services;
    }

}
