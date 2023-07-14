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

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents an <see cref="IPlugin"/>-based implementation of the <see cref="IEventStore"/> interface
/// </summary>
public class PluginEventStoreProvider
    : IEventStoreProvider
{

    /// <summary>
    /// Initializes a new <see cref="PluginEventStoreProvider"/>
    /// </summary>
    /// <param name="pluginManager">The service used to manage <see cref="IPlugin"/>s</param>
    public PluginEventStoreProvider(IPluginManager pluginManager)
    {
        this.PluginManager = pluginManager;
    }

    /// <summary>
    /// Gets the service used to manage <see cref="IPlugin"/>s
    /// </summary>
    protected IPluginManager PluginManager { get; }

    private IEventStore? _eventStore;
    /// <inheritdoc/>
    public IEventStore GetEventStore()
    {
        if(this._eventStore != null) return this._eventStore;
        var plugin = PluginManager.FindPluginAsync<IEventStoreProvider>().GetAwaiter().GetResult() ?? throw new NullReferenceException("Failed to find an event store provider plugin");
        this._eventStore = plugin.GetEventStore();
        return this._eventStore;
    }

}
