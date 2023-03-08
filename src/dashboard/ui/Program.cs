using BlazorBootstrap;
using CloudStreams.Core.Api.Client;
using CloudStreams.Dashboard;
using CloudStreams.Dashboard.Components;
using CloudStreams.Dashboard.Services;
using CloudStreams.Dashboard.StateManagement;
using CloudStreams.Gateway.Api.Client;
using CloudStreams.ResourceManagement.Api.Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(provider => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddCloudStreamsApiClient();
builder.Services.AddCloudStreamsGatewayApiClient(options => 
{
    options.BaseAddress = builder.HostEnvironment.BaseAddress;
});
builder.Services.AddCloudStreamsResourceManagementApiClient(options =>
{
    options.BaseAddress = builder.HostEnvironment.BaseAddress;
});
builder.Services.AddFlux(flux =>
{
    flux.ScanMarkupTypeAssembly<App>();
});
builder.Services.AddScoped<IApplicationLayout, ApplicationLayout>();
builder.Services.AddSingleton<IMonacoEditorHelper, MonacoEditorHelper>();
builder.Services.AddSingleton<IYamlConverter, YamlConverter>();
builder.Services.AddBlazorBootstrap();

await builder.Build().RunAsync();