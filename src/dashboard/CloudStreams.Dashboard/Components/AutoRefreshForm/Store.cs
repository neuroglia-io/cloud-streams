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

namespace CloudStreams.Dashboard.Components.AutoRefreshFormStateManagement;

/// <summary>
/// Represents a <see cref="AutoRefreshForm"/>'s <see cref="ComponentStore{TState}"/>
/// </summary>
/// <remarks>
/// Initializes a new <see cref="AutoRefreshFormStore"/>
/// </remarks>
public class AutoRefreshFormStore()
    : ComponentStore<AutoRefreshFormState>(new())
{

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="AutoRefreshFormState.Enabled"/> changes
    /// </summary>
    public IObservable<bool> Enabled => this.Select(state => state.Enabled).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="AutoRefreshFormState.Interval"/> changes
    /// </summary>
    public IObservable<int> Interval => this.Select(state => state.Interval).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to trigger refresh
    /// </summary>
    public IObservable<long> Refresh => this.Enabled
        .Select(enabled =>
            enabled 
                ? this.Interval.Select(Interval => Observable.Interval(TimeSpan.FromSeconds(Interval))).Switch()
                : Observable.Empty<long>()
        )
        .Switch()
        .DistinctUntilChanged();

    /// <summary>
    /// Sets the state's <see cref="AutoRefreshFormState.Enabled"/>
    /// </summary>
    /// <param name="enabled">The new <see cref="AutoRefreshFormState.Enabled"/> value</param>
    public void SetEnabled(bool enabled)
    {
        this.Reduce(state => state with
        {
            Enabled = enabled
        });
    }

    /// <summary>
    /// Sets the state's <see cref="AutoRefreshFormState.Interval"/>
    /// </summary>
    /// <param name="interval">The new <see cref="AutoRefreshFormState.Interval"/> value</param>
    public void SetInterval(int interval)
    {
        this.Reduce(state => state with
        {
            Interval = interval
        });
    }
}
