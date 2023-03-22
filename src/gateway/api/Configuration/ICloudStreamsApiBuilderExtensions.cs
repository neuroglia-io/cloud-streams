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

using CloudStreams.Core.Infrastructure.Configuration;
using CloudStreams.Gateway.Api.Controllers;
using CloudStreams.Gateway.Api.Services;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using CloudStreams.Gateway.Application.Configuration;
using CloudStreams.Gateway.Application.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Gateway.Api.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    /// <summary>
    /// Configures CloudStreams to use the Gateway API
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseGatewayApi(this ICloudStreamsApplicationBuilder builder)
    {
        var options = new GatewayOptions();
        builder.Configuration.AddEnvironmentVariables(GatewayOptions.EnvironmentVariablePrefix);
        builder.Configuration.Bind(options);

        builder.WithServiceName(options.Name);
        builder.Services.Configure<GatewayOptions>(builder.Configuration);
        builder.RegisterApplicationPart<CloudEventsController>();
        builder.RegisterMediationAssembly<ConsumeEventCommand>();
        builder.RegisterValidationAssembly<ConsumeEventCommand>();
        builder.Services.TryAddSingleton<CloudEventAdmissionControl>();
        builder.Services.TryAddSingleton<ICloudEventAdmissionControl>(provider => provider.GetRequiredService<CloudEventAdmissionControl>());
        builder.Services.AddHostedService<CloudEventHubDispatcher>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<CloudEventAdmissionControl>());
        builder.Services.AddSingleton<IGatewayMetrics, GatewayMetrics>();
        return builder;
    }

}
