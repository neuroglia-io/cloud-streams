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

using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Represents the base class for all component stores
/// </summary>
/// <typeparam name="TState">The type of the component store's state</typeparam>
public abstract class ComponentStore<TState>
    : IComponentStore<TState>
{
    private BehaviorSubject<TState> _Subject;
    private TState _State;
    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="ComponentStore{TState}"/>
    /// </summary>
    /// <param name="state">The store's initial state</param>
    protected ComponentStore(TState state)
    {
        this.CancellationTokenSource = new CancellationTokenSource();
        this._State = state;
        this._Subject = new(state);
    }

    /// <summary>
    /// Gets the <see cref="ComponentStore{TState}"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; }

    /// <inheritdoc/>
    public virtual Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Sets the <see cref="ComponentStore{TState}"/>'s state
    /// </summary>
    /// <param name="state">The updated state to set</param>
    protected virtual void Set(TState state)
    {
        this._State = state;
        this._Subject.OnNext(this._State);
    }

    /// <summary>
    /// Patches the <see cref="ComponentStore{TState}"/>'s state
    /// </summary>
    /// <param name="reducer">A <see cref="Func{T, TResult}"/> used to reduce the <see cref="ComponentStore{TState}"/>'s state</param>
    protected virtual void Reduce(Func<TState, TState> reducer)
    {
        this.Set(reducer(this._State));
    }

    /// <summary>
    /// Get the <see cref="ComponentStore{TState}"/>'s state
    /// </summary>
    /// <returns></returns>
    protected virtual TState Get()
    {
        return this._State;
    }

    /// <summary>
    /// Get a <see cref="ComponentStore{TState}"/>'s state slice for the provided projection
    /// </summary>
    /// <returns></returns>
    protected virtual T Get<T>(Func<TState, T> project)
    {
        return project(this._State);
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<TState> observer) => this._Subject.Throttle(TimeSpan.FromMicroseconds(1)).Subscribe(observer);

    /// <summary>
    /// Disposes of the <see cref="ComponentStore{TState}"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ComponentStore{TState}"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.CancellationTokenSource.Cancel();
                this.CancellationTokenSource.Dispose();
                this._Subject.Dispose();
            }
            this._Disposed = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the <see cref="ComponentStore{TState}"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ComponentStore{TState}"/> is being disposed of</param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    protected virtual ValueTask DisposeAsync(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.CancellationTokenSource.Cancel();
                this.CancellationTokenSource.Dispose();
                this._Subject.Dispose();
            }
            this._Disposed = true;
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

}