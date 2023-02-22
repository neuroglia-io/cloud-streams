using CloudStreams.Core.Api.Client;
using CloudStreams.Dashboard;
using CloudStreams.Dashboard.StateManagement;
using CloudStreams.Gateway.Api.Client;
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
builder.Services.AddFlux(flux =>
{
    flux.ScanMarkupTypeAssembly<App>();
});

await builder.Build().RunAsync();