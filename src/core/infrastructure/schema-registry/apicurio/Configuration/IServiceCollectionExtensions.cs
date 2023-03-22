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

using CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Configuration;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configures a new Api Curio Registry client
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup <see cref="ApiCurioRegistryClientOptions"/></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static IServiceCollection AddApiCurioRegistryClient(this IServiceCollection services, Action<ApiCurioRegistryClientOptions>? setup = null, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        var options = new ApiCurioRegistryClientOptions();
        if (setup != null) setup(options);
        services.AddSingleton(Options.Create(options));
        services.AddHttpClient(typeof(ApicurioRegistryApiClient).Name, httpClient =>
        {
            httpClient.BaseAddress = options.ServerUri;
        });
        services.Add(new(typeof(ApicurioRegistryApiClient), typeof(ApicurioRegistryApiClient), lifetime));
        services.Add(new(typeof(IApicurioRegistryApiClient), provider => provider.GetRequiredService<ApicurioRegistryApiClient>(), lifetime));
        return services;
    }

}
