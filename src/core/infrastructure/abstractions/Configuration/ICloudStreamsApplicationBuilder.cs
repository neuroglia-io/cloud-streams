using CloudStreams.Core.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace CloudStreams.Core.Infrastructure.Configuration;

/// <summary>
/// Defines fundamentals of a service used to build and configure a Cloud Streams API
/// </summary>
public interface ICloudStreamsApplicationBuilder
{

    /// <summary>
    /// Gets the current <see cref="ConfigurationManager"/>
    /// </summary>
    ConfigurationManager Configuration { get; }

    /// <summary>
    /// Gets the current <see cref="IHostEnvironment"/>
    /// </summary>
    IHostEnvironment Environment { get; }

    /// <summary>
    /// Gets the current <see cref="IServiceCollection"/>
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Registers an <see cref="Assembly"/> application part
    /// </summary>
    /// <typeparam name="TMarkup">A markup type containing by the <see cref="Assembly"/> application part to add</typeparam>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    ICloudStreamsApplicationBuilder RegisterApplicationPart<TMarkup>();

    /// <summary>
    /// Registers an <see cref="Assembly"/> to scan for mediation components such as queries, commands, etc.
    /// </summary>
    /// <typeparam name="TMarkup">A markup type containing by the <see cref="Assembly"/> to scan</typeparam>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    ICloudStreamsApplicationBuilder RegisterMediationAssembly<TMarkup>();

    /// <summary>
    /// Registers an <see cref="Assembly"/> to scan for fluent validators
    /// </summary>
    /// <typeparam name="TMarkup">A markup type containing by the <see cref="Assembly"/> to scan</typeparam>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    ICloudStreamsApplicationBuilder RegisterValidationAssembly<TMarkup>();

    /// <summary>
    /// Registers a new health check
    /// </summary>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup the health check to register</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    ICloudStreamsApplicationBuilder RegisterHealthCheck(Action<IHealthChecksBuilder> setup);

    /// <summary>
    /// Configures Cloud Streams to use the specified <see cref="ICloudEventStore"/>
    /// </summary>
    /// <typeparam name="TStore">The type of <see cref="ICloudEventStore"/> to use</typeparam>
    /// <returns>The configured <see cref="ICloudEventStore"/></returns>
    ICloudStreamsApplicationBuilder UseCloudEventStore<TStore>()
        where TStore : class, ICloudEventStore;

    /// <summary>
    /// Configures Cloud Streams to use the specified <see cref="IResourceRepository"/>
    /// </summary>
    /// <typeparam name="TRepository">The type of <see cref="IResourceRepository"/> to use</typeparam>
    /// <returns>The configured <see cref="ICloudEventStore"/></returns>
    ICloudStreamsApplicationBuilder UseResourceRepository<TRepository>()
        where TRepository : class, IResourceRepository;

    /// <summary>
    /// Configures Cloud Streams to use the specified <see cref="ISchemaRegistry"/>
    /// </summary>
    /// <typeparam name="TRegistry">The type of <see cref="ISchemaRegistry"/> to use</typeparam>
    /// <returns>The configured <see cref="ICloudEventStore"/></returns>
    ICloudStreamsApplicationBuilder UseSchemaRegistry<TRegistry>()
        where TRegistry : class, ISchemaRegistry;

    /// <summary>
    /// Builds the Cloud Streams API
    /// </summary>
    void Build();

}
