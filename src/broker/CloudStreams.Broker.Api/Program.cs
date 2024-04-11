using CloudStreams.Broker.Application.Configuration;
using CloudStreams.Broker.Application.Services;
using CloudStreams.Core;
using CloudStreams.Core.Application.Services;
using CloudStreams.Core.Resources;
using Microsoft.AspNetCore.ResponseCompression;
using Neuroglia.Data.Expressions.JQ;
using Neuroglia.Data.Infrastructure.EventSourcing.Services;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Data.PatchModel.Services;
using Neuroglia.Mediation;
using Neuroglia.Mediation.Services;
using Neuroglia.Plugins;
using Neuroglia.Security.Services;
using Neuroglia.Serialization;

CloudStreamsDefaults.Telemetry.ActivitySource = new("Cloud Streams Broker");

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables(BrokerOptions.EnvironmentVariablePrefix);

builder.Services.Configure<BrokerOptions>(builder.Configuration);
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
builder.Services.AddSerialization();
builder.Services.AddJsonSerializer();
builder.Services.AddYamlDotNetSerializer();
builder.Services.AddMediator(options =>
{
    options.UseDefaultPipelineBehavior(typeof(GuardExceptionHandlingMiddleware<,>));
    options.UseDefaultPipelineBehavior(typeof(ProblemDetailsExceptionHandlingMiddleware<,>));
});
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
builder.Services.AddSingleton<CloudEventStore>();
builder.Services.AddSingleton<ICloudEventStore>(provider => provider.GetRequiredService<CloudEventStore>());
builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<CloudEventStore>());
builder.Services.AddJQExpressionEvaluator();
builder.Services.AddSingleton<SubscriptionManager>();
builder.Services.AddSingleton<IResourceController<Subscription>>(provider => provider.GetRequiredService<SubscriptionManager>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<SubscriptionManager>());

using var app = builder.Build();

app.UseResponseCompression();
app.UseStaticFiles();

await app.RunAsync();