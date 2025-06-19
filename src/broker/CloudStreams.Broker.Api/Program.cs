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

using CloudStreams.Broker.Application.Configuration;
using CloudStreams.Broker.Application.Services;
using CloudStreams.Core;
using CloudStreams.Core.Api;
using CloudStreams.Core.Resources;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;

CloudStreamsDefaults.Telemetry.ActivitySource = new("Cloud Streams Broker");

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables(BrokerOptions.EnvironmentVariablePrefix);
builder.UseCloudStreams(builder => 
{
    builder.WithServiceName("cloud-streams-broker");
});

builder.Services.Configure<BrokerOptions>(builder.Configuration);
builder.Services.AddSingleton<SubscriptionManager>();
builder.Services.AddSingleton<IResourceController<Subscription>>(provider => provider.GetRequiredService<SubscriptionManager>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<SubscriptionManager>());

using var app = builder.Build();

app.UseResponseCompression();

await app.RunAsync();