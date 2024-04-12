using CloudStreams.Core.Api;
using CloudStreams.Core.Api.Hubs;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.UseCloudStreams(builder =>
{
    builder.UseCoreApi();
});

using var app = builder.Build();

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
    builder.SwaggerEndpoint("/api/v1/doc/oas.json", "Cloud Streams API v1");
    builder.RoutePrefix = "api/doc";
    builder.DisplayOperationId();
});

app.MapControllers();
app.MapHub<ResourceEventWatchHub>("api/resources/v1/ws/watch");
app.MapFallbackToFile("index.html");

await app.RunAsync().ConfigureAwait(false);