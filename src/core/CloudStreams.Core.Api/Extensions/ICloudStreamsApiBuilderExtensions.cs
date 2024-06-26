﻿// Copyright © 2024-Present The Cloud Streams Authors
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

using CloudStreams.Core.Api.Services;
using CloudStreams.Core.Application.Services;

namespace CloudStreams.Core.Api;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    /// <summary>
    /// Configures CloudStreams to use the Core API
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseCoreApi(this ICloudStreamsApplicationBuilder builder)
    {
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<ResourceWatchEventHubController>();
        builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ResourceWatchEventHubController>());
        builder.Services.AddHostedService<GatewayResourceController>();
        builder.Services.AddHostedService<BrokerResourceController>();
        return builder;
    }

}

