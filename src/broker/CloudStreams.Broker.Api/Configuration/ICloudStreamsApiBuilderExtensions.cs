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

using CloudStreams.Broker.Application.Configuration;
using CloudStreams.Broker.Application.Services;
using CloudStreams.Core.Infrastructure.Configuration;
using Hylo.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Broker.Api.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    /// <summary>
    /// Configures CloudStreams to use the Broker API
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseBrokerApi(this ICloudStreamsApplicationBuilder builder)
    {
        var options = new BrokerOptions();
        builder.Configuration.AddEnvironmentVariables(BrokerOptions.EnvironmentVariablePrefix);
        builder.Configuration.Bind(options);

        builder.WithServiceName(options.Name);
        builder.Services.Configure<BrokerOptions>(builder.Configuration);
        builder.Services.AddResourceController<Subscription>();
        builder.Services.TryAddSingleton<SubscriptionManager>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<SubscriptionManager>());

        return builder;
    }

}
