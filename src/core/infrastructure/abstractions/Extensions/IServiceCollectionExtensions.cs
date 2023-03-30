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

using CloudStreams.Core.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Core.Infrastructure.Configuration;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configures a new <see cref="IResourceController{TResource}"/> to control <see cref="IResource"/>s of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to control</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddResourceController<TResource>(this IServiceCollection services)
        where TResource : class, IResource, new()
    {
        services.TryAddSingleton<ResourceController<TResource>>();
        services.TryAddSingleton<IResourceController<TResource>>(provider => provider.GetRequiredService<ResourceController<TResource>>());
        services.AddHostedService(provider => provider.GetRequiredService<ResourceController<TResource>>());
        return services;
    }

    /// <summary>
    /// Adds and configures a new <see cref="IResourceMonitor{TResource}"/> to monitor the specified <see cref="IResource"/>
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to monitor</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="name">AThe name of the <see cref="IResource"/> to monitor</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> to monitor belongs to, if any</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddResourceMonitor<TResource>(this IServiceCollection services, string name, string? @namespace = null)
        where TResource : class, IResource, new()
    {
        services.AddSingleton(provider => provider.GetRequiredService<IResourceRepository>().MonitorAsync<TResource>(name, @namespace).GetAwaiter().GetResult());
        return services;
    }

}
