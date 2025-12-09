// Copyright © 2024-Present The Cloud Streams Authors
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

using CloudStreams.Core.Application;
using CloudStreams.Core.Application.Commands.Resources;
using CloudStreams.Core.Application.Services;
using EventStore.Client.Extensions.OpenTelemetry;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi.Models;
using Neuroglia.Data.Expressions.JQ;
using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Data.Infrastructure.ResourceOriented.Redis;
using Neuroglia.Data.PatchModel.Services;
using Neuroglia.Mediation.Services;
using Neuroglia.Security.Services;
using Neuroglia.Serialization.Json;
using Neuroglia.Serialization.Yaml;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;

namespace CloudStreams.Core.Api.Services;

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
        var esdbConnectionString = configuration.GetConnectionString("eventstore")!;
        var redisConnectionString = configuration.GetConnectionString("redis")!;
        this.Configuration = configuration;
        this.Environment = environment;
        this.Services = services;
        this.Logging = logging;
        this.Services.AddHttpClient();
        this.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        this.Services.AddProblemDetails();
        this.Services.AddEndpointsApiExplorer();
        this.Services.AddCoreApiCommands();
        this.Services.AddCoreApiQueries();
        this.Services.AddSerialization();
        this.Services.AddJsonSerializer();
        this.Services.AddYamlDotNetSerializer();
        this.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        this.Services.AddSingleton<IUserAccessor, HttpContextUserAccessor>();
        this.Services.AddSingleton<IUserInfoProvider, UserInfoProvider>();
        //this.Services.AddPluginProvider(this.Configuration);
        //this.Services.AddPlugin<IEventStore>();
        //this.Services.AddPlugin<IProjectionManager>();
        //this.Services.AddPlugin<IDatabase>();
        this.Services.AddEventStoreClient(esdbConnectionString);
        this.Services.AddEventStorePersistentSubscriptionsClient(esdbConnectionString);
        this.Services.AddEventStoreProjectionManagementClient(esdbConnectionString);
        this.Services.AddEsdbEventStore();
        this.Services.AddEsdbProjectionManager();
        this.Services.AddRedisDatabase(redisConnectionString);
        this.Services.AddSingleton<CloudEventStore>();
        this.Services.AddSingleton<ICloudEventStore>(provider => provider.GetRequiredService<CloudEventStore>());
        this.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<CloudEventStore>());
        this.Services.AddHostedService<Application.Services.DatabaseInitializer>();
        this.Services.AddSingleton<IAdmissionControl, AdmissionControl>();
        this.Services.AddSingleton<IVersionControl, VersionControl>();
        this.Services.AddSingleton<IResourceRepository, ResourceRepository>();
        this.Services.AddSingleton<IPatchHandler, JsonMergePatchHandler>();
        this.Services.AddSingleton<IPatchHandler, JsonPatchHandler>();
        this.Services.AddSingleton<IPatchHandler, JsonStrategicMergePatchHandler>();
        this.Services.AddJQExpressionEvaluator();
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
            builder.IncludeXmlComments(typeof(Broker).Assembly.Location.Replace(".dll", ".xml"));
        });
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
    /// Gets an <see cref="HashSet{T}"/> containing the assemblies to scan for mediation components
    /// </summary>
    protected HashSet<Assembly> ApplicationParts { get; } = [];

    /// <summary>
    /// Gets an <see cref="HashSet{T}"/> containing the assemblies to scan for mediation components
    /// </summary>
    protected HashSet<Assembly> MediationAssemblies { get; } = [typeof(CloudStreamsApplicationBuilder).Assembly];

    /// <summary>
    /// Gets an <see cref="HashSet{T}"/> containing the assemblies to scan for fluent validators
    /// </summary>
    protected HashSet<Assembly> ValidationAssemblies { get; } = [];

    /// <summary>
    /// Gets a <see cref="List{T}"/> containing the <see cref="Action{T}"/>s used to setup the application health checks
    /// </summary>
    protected List<Action<IHealthChecksBuilder>> HealthCheckConfigurations { get; } = [];

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
        this.ApplicationParts.Add(assembly);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApplicationBuilder RegisterMediationAssembly<TMarkup>()
    {
        var assembly = typeof(TMarkup).Assembly;
        this.MediationAssemblies.Add(assembly);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApplicationBuilder RegisterValidationAssembly<TMarkup>()
    {
        var assembly = typeof(TMarkup).Assembly;
        this.ValidationAssemblies.Add(assembly);
        return this;
    }

    /// <inheritdoc/>
    public virtual ICloudStreamsApplicationBuilder RegisterHealthCheck(Action<IHealthChecksBuilder> setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        this.HealthCheckConfigurations.Add(setup);
        return this;
    }

    /// <inheritdoc/>
    public virtual void Build()
    {
        var mvc = this.Services.AddControllers(options =>
        {
            options.InputFormatters.Add(new YamlInputFormatter(YamlSerializer.Default));
            options.OutputFormatters.Add(new YamlOutputFormatter(YamlSerializer.Default));
        });
        mvc.AddJsonOptions(options => JsonSerializer.DefaultOptionsConfiguration(options.JsonSerializerOptions));
        foreach(var applicationPart in this.ApplicationParts) mvc.AddApplicationPart(applicationPart);

        var healthChecks = this.Services.AddHealthChecks();
        foreach(var configureHealthChecks in this.HealthCheckConfigurations) configureHealthChecks(healthChecks);

        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName: this.ServiceName, serviceVersion: this.ServiceVersion);
        CloudStreamsDefaults.Telemetry.ActivitySource = new(this.ServiceName, this.ServiceVersion);

        this.Logging.AddOpenTelemetry(builder =>
        {
            builder.SetResourceBuilder(resourceBuilder);
            builder.IncludeFormattedMessage = true;
            builder.IncludeScopes = true;
            builder.ParseStateValues = true;
            builder.AddOtlpExporter();
        });
        this.Logging.Configure(options =>
        {
            options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId;
        });

        var telemetry = this.Services.AddOpenTelemetry();
        telemetry = telemetry.WithTracing(builder =>
        {
            builder
                .AddSource("*")
                .AddSource(this.ServiceName)
                .AddSource("Neuroglia.Data.Infrastructure.ResourceOriented")
                .AddSource("Neuroglia.Mediation")
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRedisInstrumentation()
                .AddEventStoreClientInstrumentation()
                .AddOtlpExporter();
        });
        telemetry = telemetry.WithMetrics(builder =>
        {
            builder
                .AddMeter("*")
                .AddMeter(this.ServiceName)
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter();
        });

        this.Services.AddMediator(options =>
        {
            options.ScanAssembly(typeof(CreateResourceCommand).Assembly);
            options.UseDefaultPipelineBehavior(typeof(GuardExceptionHandlingMiddleware<,>));
            options.UseDefaultPipelineBehavior(typeof(ProblemDetailsExceptionHandlingMiddleware<,>));
            this.MediationAssemblies.ToList().ForEach(a => options.ScanAssembly(a));
        });
        this.Services.AddValidatorsFromAssemblies(this.ValidationAssemblies, ServiceLifetime.Transient);
    }

}
