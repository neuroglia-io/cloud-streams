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

using BlazorBootstrap;
using CloudStreams.Core.Api.Client;
using CloudStreams.Dashboard;
using CloudStreams.Dashboard.Components;
using CloudStreams.Dashboard.StateManagement;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

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

await builder.Build().RunAsync();