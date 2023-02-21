using CloudStreams.Core.Infrastructure.Services;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CloudStreams.Core.Infrastructure.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    /// <summary>
    /// Configures Cloud Streams to use the <see href="https://kubernetes.io">Kubernetes</see> implementation of the <see cref="IResourceRepository"/> interface
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseKubernetesResourceStore(this ICloudStreamsApplicationBuilder builder)
    {
        var configuration = AppContextExtensions.RunsInKubernetes ? KubernetesClientConfiguration.InClusterConfig() : KubernetesClientConfiguration.BuildConfigFromConfigFile();
        builder.Services.AddKubernetesClient(configuration);
        builder.Services.TryAddSingleton<K8sResourceRepository>();
        builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<K8sResourceRepository>());
        builder.UseResourceRepository<K8sResourceRepository>();
        return builder;
    }

}
