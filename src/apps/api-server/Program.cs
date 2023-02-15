using CloudStreams;
using CloudStreams.Api.Configuration;
using CloudStreams.Infrastructure.Services;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.AddCloudStreamsApi(builder =>
{
    builder.UseHttpEndpoints();
    builder.UseESCloudEventStore();
    builder.UseApicurioSchemaRegistry();
    builder.UseKubernetesResourceStore();
});

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
        Title = "Cloud Streams REST API",
        Version = "v1",
        Description = "The Open API documentation for the Cloud Streams REST API",
        Contact = new()
        {
            Name = "The Cloud Streams Authors",
            Url = new Uri("https://github.com/neuroglia.io/cloudstreams/")
        }
    });
    builder.IncludeXmlComments(typeof(Channel).Assembly.Location.Replace(".dll", ".xml"));
});

var app = builder.Build();

app.UseResponseCompression();
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

app.Run();
