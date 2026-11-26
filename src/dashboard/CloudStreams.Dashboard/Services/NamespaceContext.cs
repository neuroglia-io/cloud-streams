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

using CloudStreams.Core.Api.Client.Services;
using System.Reactive.Subjects;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Represents the service that holds the namespace the dashboard shows the resources and cloud events of, which the user selects in the layout (see #215)
/// </summary>
/// <param name="api">The service used to interact with the Cloud Streams API</param>
/// <param name="logger">The service used to perform logging</param>
public class NamespaceContext(ICloudStreamsCoreApiClient api, ILogger<NamespaceContext> logger)
    : IDisposable
{

    readonly BehaviorSubject<string?> _selected = new(null);
    readonly BehaviorSubject<IReadOnlyList<string>> _namespaces = new([Namespace.DefaultNamespaceName]);

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the selected namespace, null when the resources of every namespace the user may read are shown
    /// </summary>
    public IObservable<string?> Selected => this._selected.DistinctUntilChanged();

    /// <summary>
    /// Gets the selected namespace, if any
    /// </summary>
    public string? Current => this._selected.Value;

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the namespaces the user may read
    /// </summary>
    public IObservable<IReadOnlyList<string>> Namespaces => this._namespaces;

    /// <summary>
    /// Gets the namespace new resources are created in by default: the selected one, or the default namespace
    /// </summary>
    public string ForNewResources => this.Current ?? Namespace.DefaultNamespaceName;

    /// <summary>
    /// Selects the specified namespace
    /// </summary>
    /// <param name="namespace">The namespace to select, or null to show the resources of every namespace</param>
    public void Select(string? @namespace) => this._selected.OnNext(string.IsNullOrWhiteSpace(@namespace) ? null : @namespace);

    /// <summary>
    /// Lists the namespaces the user may read again
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var namespaces = await (await api.Resources.Namespaces.ListAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Select(n => n.GetName()).ToListAsync(cancellationToken).ConfigureAwait(false);
            namespaces.Sort(StringComparer.Ordinal);
            this._namespaces.OnNext(namespaces.Count > 0 ? namespaces : [Namespace.DefaultNamespaceName]);
            if (this.Current != null && !namespaces.Contains(this.Current)) this.Select(null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to list the namespaces");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this._selected.Dispose();
        this._namespaces.Dispose();
        GC.SuppressFinalize(this);
    }

}
