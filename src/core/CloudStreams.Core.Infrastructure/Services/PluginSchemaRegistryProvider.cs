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

using Hylo;
using Hylo.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents an <see cref="IPlugin"/>-based implementation of the <see cref="IEventStoreProvider"/> interface
/// </summary>
public class PluginSchemaRegistryProvider
    : ISchemaRegistryProvider
{

    /// <summary>
    /// Initializes a new <see cref="PluginSchemaRegistryProvider"/>
    /// </summary>
    /// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="pluginManager">The service used to manage <see cref="IPlugin"/>s</param>
    public PluginSchemaRegistryProvider(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, IPluginManager pluginManager)
    {
        this.ServiceProvider = serviceProvider;
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.PluginManager = pluginManager;
    }

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the service used to perform loggi ng
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the service used to manage <see cref="IPlugin"/>s
    /// </summary>
    protected IPluginManager PluginManager { get; }

    private ISchemaRegistry? _schemaRegistry;
    /// <inheritdoc/>
    public ISchemaRegistry GetSchemaRegistry()
    {
        if (this._schemaRegistry != null) return this._schemaRegistry;
        var plugin = this.PluginManager.FindPluginAsync<ISchemaRegistryProvider>().GetAwaiter().GetResult();
        if(plugin == null)
        {
            this.Logger.LogWarning("No schema registry provider plugin found. Falling back to the memory based schema provider");
            this._schemaRegistry = ActivatorUtilities.CreateInstance<MemoryCacheSchemaRegistry>(this.ServiceProvider, Array.Empty<object>());
        }
        else
        {
            this._schemaRegistry = plugin.GetSchemaRegistry();
        }
        return this._schemaRegistry;
    }

}