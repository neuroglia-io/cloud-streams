using CloudStreams.Broker.Application.Configuration;
using CloudStreams.Broker.Application.Services;
using CloudStreams.Core;
using CloudStreams.Core.Api;
using CloudStreams.Core.Resources;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;

CloudStreamsDefaults.Telemetry.ActivitySource = new("Cloud Streams Broker");

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables(BrokerOptions.EnvironmentVariablePrefix);
builder.UseCloudStreams(builder => { });

builder.Services.Configure<BrokerOptions>(builder.Configuration);
builder.Services.AddSingleton<SubscriptionManager>();
builder.Services.AddSingleton<IResourceController<Subscription>>(provider => provider.GetRequiredService<SubscriptionManager>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<SubscriptionManager>());

using var app = builder.Build();

app.UseResponseCompression();

await app.RunAsync();