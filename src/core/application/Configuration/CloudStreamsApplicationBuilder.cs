using CloudStreams.Core.Application.Services;
using CloudStreams.Core.Infrastructure.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace CloudStreams.Core.Application.Configuration;

/// <summary>
/// Represents the default implementation of the <see cref="ICloudStreamsApplicationBuilder"/>
/// </summary>
public class CloudStreamsApplicationBuilder
    : ICloudStreamsApplicationBuilder
{

    /// <summary>
    /// Initializes a new <see cref="CloudStreamsApplicationBuilder"/>
    /// </summary>
    /// <param name="configuration">The current <see cref="IConfiguration"/></param>
    /// <param name="environment">The current <see cref="IHostEnvironment"/></param>
    /// <param name="services">The current <see cref="IServiceCollection"/></param>
    public CloudStreamsApplicationBuilder(IConfiguration configuration, IHostEnvironment environment, IServiceCollection services)
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
    protected HashSet<Assembly> ApplicationParts { get; } = new();

    /// <summary>
    /// Gets a <see cref="List{T}"/> containing the assemblies to scan for mediation components
    /// </summary>
    protected HashSet<Assembly> MediationAssemblies { get; } = new() { typeof(CloudStreamsApplicationBuilder).Assembly };

    /// <inheritdoc/>
    public virtual ICloudStreamsApplicationBuilder RegisterApplicationPart<TMarkup>()
    {
        var assembly = typeof(TMarkup).Assembly;
        if (!this.ApplicationParts.Contains(assembly)) this.ApplicationParts.Add(assembly);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApplicationBuilder RegisterMediationAssembly<TMarkup>()
    {
        var assembly = typeof(TMarkup).Assembly;
        if (!this.MediationAssemblies.Contains(assembly)) this.MediationAssemblies.Add(assembly);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApplicationBuilder UseCloudEventStore<TStore>()
        where TStore : class, ICloudEventStore
    {
        this.CloudEventStoreType = typeof(TStore);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApplicationBuilder UseResourceRepository<TRepository>()
         where TRepository : class, IResourceRepository
    {
        this.ResourceRepositoryType = typeof(TRepository);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApplicationBuilder UseSchemaRegistry<TRegistry>()
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

        var mvc = this.Services.AddControllers(options =>
        {
            options.InputFormatters.Add(new YamlInputFormatter());
            options.OutputFormatters.Add(new YamlOutputFormatter());
        });
        mvc.AddJsonOptions(options =>
        {
            Serializer.Json.DefaultOptionsConfiguration?.Invoke(options.JsonSerializerOptions);
        });
        foreach(var applicationPart in this.ApplicationParts)
        {
            mvc.AddApplicationPart(applicationPart);
        }

        this.Services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssemblies(this.MediationAssemblies.ToArray());
        });
        this.Services.AddHealthChecks();
        this.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        this.Services.AddProblemDetails();
        this.Services.AddEndpointsApiExplorer();
        this.Services.TryAddSingleton<IAuthorizationManager, AuthorizationManager>();
        this.Services.TryAddSingleton<ICloudEventAdmissionControl, CloudEventAdmissionControl>();
        this.Services.TryAddSingleton<ISchemaGenerator, SchemaGenerator>();
        this.Services.TryAddSingleton(provider => (ICloudEventStore)provider.GetRequiredService(this.CloudEventStoreType));
        this.Services.TryAddSingleton(provider => (IResourceRepository)provider.GetRequiredService(this.ResourceRepositoryType));
        this.Services.TryAddSingleton(provider => (ISchemaRegistry)provider.GetRequiredService(this.SchemaRegistryType));
    }

}
