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

using Microsoft.Extensions.DependencyInjection;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IResourceMonitorProvider"/> interface
/// </summary>
public class ResourceMonitorProvider
    : IResourceMonitorProvider
{

    /// <summary>
    /// Initializes a new <see cref="ResourceMonitorProvider"/>
    /// </summary>
    /// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
    public ResourceMonitorProvider(IServiceProvider serviceProvider)
    {
        this.ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <inheritdoc/>
    public virtual IResourceMonitor<TResource> GetResourceMonitor<TResource>(string name, string? @namespace = null)
        where TResource : class, IResource, new()
    {
        var monitor = this.ServiceProvider.GetServices<IResourceMonitor<TResource>>().FirstOrDefault(m => m.Resource.GetName() == name && m.Resource.GetNamespace() == @namespace);
        if (monitor == null) throw new NullReferenceException($"No resource monitor were configured for the resource '{new ResourceReference<TResource>(name, @namespace)}'");
        return monitor;
    }

}
