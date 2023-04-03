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

using CloudStreams.Broker.Api.Configuration;
using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Application.Configuration;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);
builder.UseCloudStreams(builder =>
{
    builder.UseCoreApi();
    builder.UseBrokerApi();
    builder.UseESCloudEventStore();
    builder.UseApicurioSchemaRegistry();
    builder.UseKubernetesResourceStore();
    builder.UseJavaScriptExpressionEvaluator();
});

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.UseWebAssemblyDebugging();
else app.UseExceptionHandler("/error");

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
    builder.SwaggerEndpoint("/api/v1/doc/oas.json", "Cloud Streams Broker API v1");
    builder.RoutePrefix = "api/doc";
    builder.DisplayOperationId();
});

app.MapControllers();
app.MapFallbackToFile("index.html");

await app.RunAsync();
