using CloudStreams.Api.Configuration;
using CloudStreams.Core.Application.Configuration;
using CloudStreams.Core.Infrastructure.Services;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.UseCloudStreams(builder =>
{
    builder.UseGatewayApi();
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
    builder.IncludeXmlComments(typeof(Channel).Assembly.Location.Replace(".dll", ".xml"));
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
app.MapFallbackToFile("index.html");

var sub = await app.Services.GetRequiredService<ICloudEventStore>().SubscribeAsync();
sub.Subscribe(e =>
{
    Console.WriteLine($"Yaaai, a new cloud event has been received:");
    Console.WriteLine(Serializer.Yaml.Serialize(e));
});

app.Run();
