using CloudStreams.Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Neuroglia.Serialization;
using System.Reflection;

namespace CloudStreams.Api.Configuration;

/// <summary>
/// Represents the default implementation of the <see cref="ICloudStreamsApiBuilder"/>
/// </summary>
public class CloudStreamsApiBuilder
    : ICloudStreamsApiBuilder
{

    /// <summary>
    /// Initializes a new <see cref="CloudStreamsApiBuilder"/>
    /// </summary>
    /// <param name="configuration">The current <see cref="IConfiguration"/></param>
    /// <param name="environment">The current <see cref="IHostEnvironment"/></param>
    /// <param name="services">The current <see cref="IServiceCollection"/></param>
    public CloudStreamsApiBuilder(IConfiguration configuration, IHostEnvironment environment, IServiceCollection services)
    {
        this.Configuration = configuration;
        this.Environment = environment;
        this.Services = services;
    }

    /// <inheritdoc/>
    public IConfiguration Configuration { get; }

    /// <inheritdoc/>
    public IHostEnvironment Environment { get; }

    /// <inheritdoc/>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets the type of <see cref="ICloudEventStore"/> to use
    /// </summary>
    protected Type? CloudEventStoreType { get; set; }

    /// <summary>
    /// Gets the type of <see cref="IResourceRepository"/> to use
    /// </summary>
    protected Type? ResourceRepositoryType { get; set; }

    /// <summary>
    /// Gets the type of <see cref="ISchemaRegistry"/> to use
    /// </summary>
    protected Type? SchemaRegistryType { get; set; }

    /// <summary>
    /// Gets a <see cref="List{T}"/> containing the assemblies to scan for mediation components
    /// </summary>
    protected List<Assembly> MediationAssemblies { get; } = new() { typeof(CloudStreamsApiBuilder).Assembly };

    /// <inheritdoc/>
    public virtual ICloudStreamsApiBuilder RegisterMediationAssembly<TMarkup>()
    {
        var assembly = typeof(TMarkup).Assembly;
        if (!this.MediationAssemblies.Contains(assembly)) this.MediationAssemblies.Add(assembly);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApiBuilder UseCloudEventStore<TStore>()
        where TStore : class, ICloudEventStore
    {
        this.CloudEventStoreType = typeof(TStore);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApiBuilder UseResourceRepository<TRepository>()
         where TRepository : class, IResourceRepository
    {
        this.ResourceRepositoryType = typeof(TRepository);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApiBuilder UseSchemaRegistry<TRegistry>()
         where TRegistry : class, ISchemaRegistry
    {
        this.SchemaRegistryType = typeof(TRegistry);
        return this;
    }

    /// <inheritdoc/>
    public virtual void Build()
    {
        if (this.CloudEventStoreType == null) throw new Exception("Invalid Cloud Streams API configuration: the cloud event store type must be set");
        if (this.ResourceRepositoryType == null) throw new Exception("Invalid Cloud Streams API configuration: the resource repository type must be set");
        if (this.SchemaRegistryType == null) throw new Exception("Invalid Cloud Streams API configuration: the schema registry type must be set");
        this.Services.AddMediatR(this.MediationAssemblies.ToArray());
        this.Services.TryAddSingleton<IAuthorizationManager, AuthorizationManager>();
        this.Services.TryAddSingleton<ICloudEventAdmissionControl, CloudEventAdmissionControl>();
        this.Services.TryAddSingleton<ISchemaGenerator, SchemaGenerator>();
        this.Services.TryAddSingleton(provider => (ICloudEventStore)provider.GetRequiredService(this.CloudEventStoreType));
        this.Services.TryAddSingleton(provider => (IResourceRepository)provider.GetRequiredService(this.ResourceRepositoryType));
        this.Services.TryAddSingleton(provider => (ISchemaRegistry)provider.GetRequiredService(this.SchemaRegistryType));

        this.Services.AddJsonSerializer(Serializer.Json.DefaultOptionsConfiguration); //todo: aim to remove all neuroglia dependencies (Apicurio)
    }

}
