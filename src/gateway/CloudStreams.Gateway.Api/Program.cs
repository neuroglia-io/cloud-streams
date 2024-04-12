using CloudStreams.Core;
using CloudStreams.Core.Api;
using CloudStreams.Core.Api.Hubs;
using CloudStreams.Core.Application.Services;
using CloudStreams.Gateway.Api.Hubs;
using CloudStreams.Gateway.Api.Services;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using CloudStreams.Gateway.Application.Configuration;
using CloudStreams.Gateway.Application.Services;
using CloudStreams.Gateway.Controllers;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Swashbuckle.AspNetCore.SwaggerUI;

CloudStreamsDefaults.Telemetry.ActivitySource = new("Cloud Streams Gateway");

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables(GatewayOptions.EnvironmentVariablePrefix);
builder.UseCloudStreams(builder =>
{
    builder.UseCoreApi();
    builder.RegisterApplicationPart<CloudEventsController>();
    builder.RegisterMediationAssembly<ConsumeEventCommand>();
});

builder.Services.Configure<GatewayOptions>(builder.Configuration);
builder.Services.AddSingleton<CloudEventAdmissionControl>();
builder.Services.AddSingleton<ICloudEventAdmissionControl>(provider => provider.GetRequiredService<CloudEventAdmissionControl>());
builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<CloudEventAdmissionControl>());
builder.Services.AddSingleton<ICloudEventAuthorizationManager, CloudEventAuthorizationManager>();
builder.Services.AddHostedService<CloudEventHubDispatcher>();
builder.Services.AddSingleton<IJsonSchemaGenerator, JsonSchemaGenerator>();
builder.Services.AddSingleton<IJsonSchemaRegistry, MemoryJsonSchemaRegistry>();
builder.Services.AddSingleton<IGatewayMetrics, GatewayMetrics>();

using var app = builder.Build();

app.UseResponseCompression();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.UseHealthChecks("/healthz", new HealthCheckOptions()
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
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
app.MapHub<ResourceEventWatchHub>("api/ws/resources/watch");
app.MapHub<CloudEventHub>("api/ws/events");
app.MapFallbackToFile("index.html");

await app.RunAsync();