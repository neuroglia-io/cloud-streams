using k8s;
using Microsoft.Extensions.DependencyInjection;

namespace CloudStreams.Core.Infrastructure.Configuration;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configures the Kubernets API client
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="configuration">The <see cref="KubernetesClientConfiguration"/> to use</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddKubernetesClient(this IServiceCollection services, KubernetesClientConfiguration configuration)
    {
        services.AddSingleton(provider => new Kubernetes(configuration));
        return services;
    }

}
