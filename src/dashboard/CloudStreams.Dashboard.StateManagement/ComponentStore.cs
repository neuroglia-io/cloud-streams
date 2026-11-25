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
    readonly Queue<TState> _pending = new();
    bool _delivering;

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
    /// <remarks>
    /// Observers often update the state as they observe it. Such an update is delivered once the state being delivered has reached every observer, rather than
    /// in the middle of its delivery: otherwise, the observers that had not received the older state yet would receive it after the newer one, and end on it. Every
    /// state is delivered, in order (see #199)
    /// </remarks>
    protected virtual void Set(TState state)
    {
        lock (this._pending)
        {
            this._State = state;
            this._pending.Enqueue(state);
            if (this._delivering) return;
            this._delivering = true;
        }
        try
        {
            while (true)
            {
                TState next;
                lock (this._pending)
                {
                    if (!this._pending.TryDequeue(out next!))
                    {
                        this._delivering = false;
                        return;
                    }
                }
                this._Subject.OnNext(next);
            }
        }
        catch
        {
            lock (this._pending)
            {
                this._pending.Clear();
                this._delivering = false;
            }
            throw;
        }
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
    /// <remarks>Every state is observed: throttling, even briefly, would drop the intermediate ones (see #199)</remarks>
    public virtual IDisposable Subscribe(IObserver<TState> observer) => this._Subject.Subscribe(observer);

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