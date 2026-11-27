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

using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Api.Services;
using CloudStreams.Core.Application.Validation;
using CloudStreams.Operator.Admission;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Data.PatchModel.Services;
using Neuroglia.Serialization;

// the one-off upgrade from the Redis mode, e.g. 'migrate --redis redis:6379', rather than the operator (see #283)
if (args.Length > 0 && args[0] == MigrationCommand.Name) return await MigrationCommand.RunAsync(args[1..]).ConfigureAwait(false);

var builder = WebApplication.CreateBuilder(args);
// the Helm chart renders the operator's settings as a JSON file, since some of their keys, e.g. those of annotations, can't be names of environment variables
var settings = Environment.GetEnvironmentVariable("CLOUDSTREAMS_OPERATOR_SETTINGS");
if (!string.IsNullOrWhiteSpace(settings)) builder.Configuration.AddJsonFile(settings, optional: false, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

var operatorOptions = new OperatorOptions();
builder.Configuration.GetSection(OperatorOptions.ConfigurationSection).Bind(operatorOptions);
operatorOptions.Validate();
builder.Services.Configure<OperatorOptions>(builder.Configuration.GetSection(OperatorOptions.ConfigurationSection));
// the operator reads the same options as the processes it deploys, e.g. which Kubernetes namespace the 'default' namespace of Cloud Streams is (see #277)
var resourceStore = new ResourceStoreOptions() { Store = ResourceStoreType.Kubernetes };
builder.Configuration.GetSection(ResourceStoreOptions.ConfigurationSection).Bind(resourceStore);
resourceStore.Validate();
builder.Services.AddSingleton(resourceStore.Kubernetes);
builder.Services.AddSingleton<IKubernetes>(_ => new Kubernetes(KubernetesClientConfigurationFactory.Create(resourceStore.Kubernetes)));

builder.Services.AddSerialization();
builder.Services.AddJsonSerializer();
builder.Services.AddSingleton<IPatchHandler, JsonMergePatchHandler>();
builder.Services.AddSingleton<IPatchHandler, JsonPatchHandler>();
builder.Services.AddSingleton<IPatchHandler, JsonStrategicMergePatchHandler>();
builder.Services.AddSingleton<IResourceValidator, CloudStreamsResourceValidator>();
builder.Services.AddSingleton<AdmissionWebhook>();

builder.Services.AddSingleton<OperatorLeadership>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<OperatorLeadership>());
builder.Services.AddSingleton(provider => new WorkloadFactory(provider.GetRequiredService<IOptions<OperatorOptions>>().Value, resourceStore.Kubernetes));
builder.Services.AddHostedService<WorkloadReconciler>();
builder.Services.AddHostedService<DeclaredResourceProvisioner>();
builder.Services.AddHostedService<WebhookCertificateIssuer>();
builder.Services.AddSingleton<WebhookCertificateProvider>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<WebhookCertificateProvider>());

builder.Services.AddHealthChecks().AddCheck<WebhookCertificateHealthCheck>("webhook-certificate", tags: ["ready"]);

// the health endpoints are served over HTTP, and the admission webhook over HTTPS, with a certificate that is reloaded when it is rotated (see #278)
var httpPort = int.TryParse(builder.Configuration["Operator:Port"], out var port) ? port : 8080;
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(httpPort);
    if (!operatorOptions.Webhook.Enabled) return;
    kestrel.ListenAnyIP(operatorOptions.Webhook.Port, listen => listen.UseHttps(new HttpsConnectionAdapterOptions()
    {
        ServerCertificateSelector = (_, _) => kestrel.ApplicationServices.GetRequiredService<WebhookCertificateProvider>().Certificate
    }));
});

var app = builder.Build();

app.MapHealthChecks("/healthz/live", new() { Predicate = _ => false });
app.MapHealthChecks("/healthz", new() { Predicate = check => check.Tags.Contains("ready") });

var reviewSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
app.MapPost("/validate", async (HttpContext context, AdmissionWebhook webhook) =>
{
    // the webhook is only served over HTTPS. The host the API server calls carries the port of the service, not the container's: the local port tells them apart
    if (context.Connection.LocalPort != operatorOptions.Webhook.Port) return Results.NotFound();
    var review = await JsonSerializer.DeserializeAsync<CloudStreams.Operator.Admission.AdmissionReview>(context.Request.Body, reviewSerializerOptions, context.RequestAborted).ConfigureAwait(false);
    if (review?.Request == null) return Results.BadRequest("The body must be an 'admission.k8s.io/v1' AdmissionReview with a request");
    var answer = await webhook.ReviewAsync(review, context.RequestAborted).ConfigureAwait(false);
    return Results.Json(answer, reviewSerializerOptions);
});

await app.RunAsync();
return 0;
