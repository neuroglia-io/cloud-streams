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
    builder.RegisterApplicationPart<EventsController>();
    builder.RegisterMediationAssembly<ConsumeEventCommand>();
});

builder.Services.Configure<GatewayOptions>(builder.Configuration);
builder.Services.AddHostedService<DatabaseProvisioner>();
builder.Services.AddSingleton<CloudEventAdmissionControl>();
builder.Services.AddSingleton<ICloudEventAdmissionControl>(provider => provider.GetRequiredService<CloudEventAdmissionControl>());
builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<CloudEventAdmissionControl>());
builder.Services.AddSingleton<ICloudEventAuthorizationManager, CloudEventAuthorizationManager>();
builder.Services.AddHostedService<CloudEventHubDispatcher>();
builder.Services.AddSingleton<IJsonSchemaGenerator, JsonSchemaGenerator>();
builder.Services.AddSingleton<IJsonSchemaRegistry, MemoryJsonSchemaRegistry>();
builder.Services.AddSingleton<IGatewayMetrics, GatewayMetrics>();

using var app = builder.Build();

if (app.Environment.IsDevelopment()) app.UseWebAssemblyDebugging();
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
app.MapMcp("mcp");
app.MapFallbackToFile("index.html");

await app.RunAsync();