// Copyright © 2023-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CloudStreams.Core.Application.Services;
using CloudStreams.Core.Data.Models;
using CloudStreams.Core.Infrastructure.Services;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics.Metrics;
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
    /// <param name="configuration">The current <see cref="ConfigurationManager"/></param>
    /// <param name="environment">The current <see cref="IHostEnvironment"/></param>
    /// <param name="services">The current <see cref="IServiceCollection"/></param>
    /// <param name="logging">The service used to configure and build logging</param>
    public CloudStreamsApplicationBuilder(ConfigurationManager configuration, IHostEnvironment environment, IServiceCollection services, ILoggingBuilder logging)
    {
        this.Configuration = configuration;
        this.Environment = environment;
        this.Services = services;
        this.Logging = logging;
    }

    /// <inheritdoc/>
    public ConfigurationManager Configuration { get; }

    /// <inheritdoc/>
    public IHostEnvironment Environment { get; }

    /// <inheritdoc/>
    public ILoggingBuilder Logging { get; }

    /// <inheritdoc/>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets the Cloud Streams application service name
    /// </summary>
    protected string ServiceName { get; set; } = "cloud-streams";

    /// <summary>
    /// Gets the Cloud Streams application service version
    /// </summary>
    protected string? ServiceVersion { get; set; }

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
    /// Gets an <see cref="HashSet{T}"/> containing the assemblies to scan for mediation components
    /// </summary>
    protected HashSet<Assembly> ApplicationParts { get; } = new();

    /// <summary>
    /// Gets an <see cref="HashSet{T}"/> containing the assemblies to scan for mediation components
    /// </summary>
    protected HashSet<Assembly> MediationAssemblies { get; } = new() { typeof(CloudStreamsApplicationBuilder).Assembly };

    /// <summary>
    /// Gets a <see cref="List{T}"/> containing the types of the <see cref="IPipelineBehavior{TRequest, TResponse}"/>s to register
    /// </summary>
    protected List<Type> MediationPipelineBehaviors { get; } = new();

    /// <summary>
    /// Gets an <see cref="HashSet{T}"/> containing the assemblies to scan for fluent validators
    /// </summary>
    protected HashSet<Assembly> ValidationAssemblies { get; } = new() {  };

    /// <summary>
    /// Gets a <see cref="List{T}"/> containing the <see cref="Action{T}"/>s used to setup the application health checks
    /// </summary>
    protected List<Action<IHealthChecksBuilder>> HealthCheckConfigurations { get; } = new();

    /// <inheritdoc/>
    public virtual ICloudStreamsApplicationBuilder WithServiceName(string name, string? version = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        this.ServiceName = name;
        this.ServiceVersion = version;
        return this;
    }

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
    public virtual ICloudStreamsApplicationBuilder RegisterValidationAssembly<TMarkup>()
    {
        var assembly = typeof(TMarkup).Assembly;
        if (!this.ValidationAssemblies.Contains(assembly)) this.ValidationAssemblies.Add(assembly);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApplicationBuilder RegisterHealthCheck(Action<IHealthChecksBuilder> setup)
    {
        if (setup == null) throw new ArgumentNullException(nameof(setup));
        this.HealthCheckConfigurations.Add(setup);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApplicationBuilder RegisterMediationPipelineBehavior(Type behaviorType)
    {
        if (behaviorType == null) throw new ArgumentNullException(nameof(behaviorType));
        if (behaviorType.GetGenericType(typeof(IPipelineBehavior<,>)) == null) throw new ArgumentException($"Failed to cast the specified type to type '{typeof(IPipelineBehavior<,>)}'", nameof(behaviorType));
        this.MediationPipelineBehaviors.Add(behaviorType);
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

        var healthChecks = this.Services.AddHealthChecks();
        foreach(var configureHealthChecks in this.HealthCheckConfigurations)
        {
            configureHealthChecks(healthChecks);
        }

        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName: this.ServiceName, serviceVersion: this.ServiceVersion);
        CloudStreamsTelemetry.ActivitySource = new(this.ServiceName, this.ServiceVersion);

        this.Logging.AddOpenTelemetry(builder =>
        {
            builder.SetResourceBuilder(resourceBuilder);
            builder.IncludeFormattedMessage = true;
            builder.IncludeScopes = true;
            builder.ParseStateValues = true;
            builder.AddOtlpExporter();
        });

        var telemetry = this.Services.AddOpenTelemetry();
        telemetry = telemetry.WithTracing(builder =>
        {
            builder
                .AddSource(this.ServiceName)
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter();
        });
        telemetry = telemetry.WithMetrics(builder =>
        {
            builder
                .AddMeter(this.ServiceName)
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter();
        });

        this.Services.AddSignalR();
        this.Services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssemblies(this.MediationAssemblies.ToArray());
            options.BehaviorsToRegister.Add(new(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingPipelineBehavior<,>), ServiceLifetime.Transient));
            this.MediationPipelineBehaviors.ForEach(t => options.BehaviorsToRegister.Add(new(typeof(IPipelineBehavior<,>), t, ServiceLifetime.Transient)));
        });
        this.Services.AddValidatorsFromAssemblies(this.ValidationAssemblies, ServiceLifetime.Transient);
        this.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        this.Services.AddProblemDetails();
        this.Services.AddEndpointsApiExplorer();
        this.Services.TryAddSingleton<IAuthorizationManager, AuthorizationManager>();
        this.Services.TryAddSingleton<ISchemaGenerator, SchemaGenerator>();
        this.Services.TryAddSingleton(provider => (ICloudEventStore)provider.GetRequiredService(this.CloudEventStoreType));
        this.Services.TryAddSingleton(provider => (IResourceRepository)provider.GetRequiredService(this.ResourceRepositoryType));
        this.Services.TryAddSingleton(provider => (ISchemaRegistry)provider.GetRequiredService(this.SchemaRegistryType));
        this.Services.AddSwaggerGen(builder =>
        {
            builder.CustomOperationIds(o =>
            {
                var action = (ControllerActionDescriptor)o.ActionDescriptor;
                return $"{action.ActionName}".ToCamelCase();
            });
            builder.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
            builder.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Cloud Streams REST API",
                Version = "v1",
                Description = "The Open API documentation for the Cloud Streams REST API",
                License = new OpenApiLicense()
                {
                    Name = "Apache-2.0",
                    Url = new("https://raw.githubusercontent.com/neuroglia-io/cloud-streams/main/LICENSE")
                },
                Contact = new()
                {
                    Name = "The Cloud Streams Authors",
                    Url = new Uri("https://github.com/neuroglia-io/cloud-streams")
                }
            });
            builder.IncludeXmlComments(typeof(Subscription).Assembly.Location.Replace(".dll", ".xml"));
        });
    }

}
