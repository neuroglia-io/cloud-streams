using CloudStreams.Core.Application.Configuration;
using CloudStreams.Gateway.Api.Configuration;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.UseCloudStreams(builder =>
{
    builder.UseGatewayApi();
    builder.UseESCloudEventStore();
    builder.UseApicurioSchemaRegistry();
    builder.UseKubernetesResourceStore();
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
    builder.SwaggerEndpoint("/api/v1/doc/oas.json", "Synapse API v1");
    builder.RoutePrefix = "api/doc";
    builder.DisplayOperationId();
});

app.MapControllers();
app.MapHubs();
app.MapFallbackToFile("index.html");

await app.RunAsync();
