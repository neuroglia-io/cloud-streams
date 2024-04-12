using CloudStreams.Core.Api.Client;
using CloudStreams.Dashboard;
using CloudStreams.Dashboard.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Neuroglia.Serialization;
using Neuroglia.Serialization.Json.Converters;
using Neuroglia.Serialization.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.Configure(Neuroglia.Serialization.Json.JsonSerializer.DefaultOptionsConfiguration);
builder.Services.AddSerialization();
builder.Services.AddJsonSerializer();
builder.Services.AddYamlDotNetSerializer();
builder.Services.AddScoped(provider => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddCloudStreamsCoreApiClient(options =>
{
    options.BaseAddress = builder.HostEnvironment.BaseAddress;
});
builder.Services.AddFlux(flux =>
{
    flux.ScanMarkupTypeAssembly<App>();
});
builder.Services.AddScoped<IApplicationLayout, ApplicationLayout>();
builder.Services.AddSingleton<IMonacoEditorHelper, MonacoEditorHelper>();
builder.Services.AddSingleton<MonacoInterop>();
builder.Services.AddSingleton<EventDropsInterop>();
builder.Services.AddBlazorBootstrap();

var app = builder.Build();

await app.RunAsync();