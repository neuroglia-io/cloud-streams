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

using CloudStreams.Gateway.Api.Client.Configuration;
using CloudStreams.Gateway.Api.Client.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CloudStreams.Gateway.Api.Client;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configures a new <see cref="ICloudStreamsGatewayApiClient"/>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup the <see cref="CloudStreamGatewayApiClientOptions"/> to use</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddCloudStreamsGatewayApiClient(this IServiceCollection services, Action<CloudStreamGatewayApiClientOptions> setup)
    {
        services.Configure(setup);
        services.TryAddScoped<ICloudStreamsGatewayApiClient, CloudStreamsGatewayApiClient>();
        services.TryAddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<CloudStreamGatewayApiClientOptions>>().Value;
            var connection = new HubConnectionBuilder()
                .WithUrl($"{options.BaseAddress}api/gateway/v1/ws/cloud-events")
                .WithAutomaticReconnect()
                .Build();
            return new CloudEventHubClient(connection);
        });
        return services;
    }

}