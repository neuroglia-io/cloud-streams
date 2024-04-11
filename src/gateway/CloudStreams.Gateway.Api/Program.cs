using CloudStreams.Core;
using CloudStreams.Core.Api;
using CloudStreams.Core.Application;
using CloudStreams.Core.Application.Commands.Resources;
using CloudStreams.Core.Application.Services;
using CloudStreams.Core.Resources;
using CloudStreams.Gateway.Api.Hubs;
using CloudStreams.Gateway.Api.Services;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using CloudStreams.Gateway.Application.Configuration;
using CloudStreams.Gateway.Application.Services;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi.Models;
using Neuroglia.Data.Expressions.JQ;
using Neuroglia.Data.Infrastructure.EventSourcing.Services;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Data.PatchModel.Services;
using Neuroglia.Mediation;
using Neuroglia.Mediation.Services;
using Neuroglia.Plugins;
using Neuroglia.Security.Services;
using Neuroglia.Serialization;
using Neuroglia.Serialization.Json;
using Swashbuckle.AspNetCore.SwaggerUI;

CloudStreamsDefaults.Telemetry.ActivitySource = new("Cloud Streams Gateway");

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables(GatewayOptions.EnvironmentVariablePrefix);
builder.Services.Configure<GatewayOptions>(builder.Configuration);
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<IUserAccessor, HttpContextUserAccessor>();
builder.Services.AddSingleton<IUserInfoProvider, UserInfoProvider>();
builder.Services.AddControllers()
    .AddJsonOptions(options => JsonSerializer.DefaultOptionsConfiguration(options.JsonSerializerOptions))
    .AddApplicationPart(typeof(ApiController).Assembly);
builder.Services.AddSignalR();
builder.Services.AddSerialization();
builder.Services.AddJsonSerializer();
builder.Services.AddYamlDotNetSerializer();
builder.Services.AddProblemDetails();
builder.Services.AddMediator(options =>
{
    options.ScanAssembly(typeof(CreateResourceCommand).Assembly);
    options.ScanAssembly(typeof(ConsumeEventCommand).Assembly);
    options.UseDefaultPipelineBehavior(typeof(GuardExceptionHandlingMiddleware<,>));
    options.UseDefaultPipelineBehavior(typeof(ProblemDetailsExceptionHandlingMiddleware<,>));
});
builder.Services.AddCoreApiCommands();
builder.Services.AddCoreApiQueries();
builder.Services.AddPluginProvider(builder.Configuration);
builder.Services.AddPlugin<IEventStore>();
builder.Services.AddPlugin<IProjectionManager>();
builder.Services.AddPlugin<IDatabase>();
builder.Services.AddSingleton<IRepository, Repository>();
builder.Services.AddSingleton<IAdmissionControl, AdmissionControl>();
builder.Services.AddSingleton<IVersionControl, VersionControl>();
builder.Services.AddHostedService<CloudStreams.Core.Application.Services.DatabaseInitializer>();
builder.Services.AddSingleton<IPatchHandler, JsonMergePatchHandler>();
builder.Services.AddSingleton<IPatchHandler, JsonPatchHandler>();
builder.Services.AddSingleton<IPatchHandler, JsonStrategicMergePatchHandler>();
builder.Services.AddSingleton<CloudEventAdmissionControl>();
builder.Services.AddSingleton<ICloudEventAdmissionControl>(provider => provider.GetRequiredService<CloudEventAdmissionControl>());
builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<CloudEventAdmissionControl>());
builder.Services.AddSingleton<ICloudEventAuthorizationManager, CloudEventAuthorizationManager>();
builder.Services.AddSingleton<CloudEventStore>();
builder.Services.AddSingleton<ICloudEventStore>(provider => provider.GetRequiredService<CloudEventStore>());
builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<CloudEventStore>());
builder.Services.AddHostedService<CloudEventHubDispatcher>();
builder.Services.AddSingleton<IJsonSchemaGenerator, JsonSchemaGenerator>();
builder.Services.AddSingleton<IJsonSchemaRegistry, MemoryJsonSchemaRegistry>();
builder.Services.AddSingleton<IGatewayMetrics, GatewayMetrics>();
builder.Services.AddJQExpressionEvaluator();
builder.Services.AddSwaggerGen(builder =>
{
    builder.CustomOperationIds(o =>
    {
        var action = (ControllerActionDescriptor)o.ActionDescriptor;
        return $"{action.ActionName}".ToCamelCase();
    });
    builder.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
    builder.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Cloud Streams Gateway REST API",
        Version = "v1",
        Description = "The Open API documentation for the Cloud Streams Gateway REST API",
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

using var app = builder.Build();

app.UseResponseCompression();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.UseSwagger(builder =>
{
    builder.RouteTemplate = "api/{documentName}/doc/oas.{json|yaml}";
});
app.UseSwaggerUI(builder =>
{
    builder.DocExpansion(DocExpansion.None);
    builder.SwaggerEndpoint("/api/v1/doc/oas.json", "Cloud Streams Gateway API v1");
    builder.RoutePrefix = "api/doc";
    builder.DisplayOperationId();
});
app.MapControllers();
app.MapHub<CloudEventHub>("api/ws/events");
app.MapFallbackToFile("index.html");

await app.RunAsync();