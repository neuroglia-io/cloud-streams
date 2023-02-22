using CloudStreams.Dashboard.StateManagement;
using Microsoft.AspNetCore.Components;

namespace CloudStreams.Dashboard;

/// <summary>
/// Represents the base class for all statefull <see cref="ComponentBase"/> implementations
/// </summary>
/// <typeparam name="TStore">The type of the store used to manage the component's state</typeparam>
/// <typeparam name="TState">The type of the component's state</typeparam>
public abstract class StatefulComponent<TStore, TState>
    : ComponentBase, IDisposable
    where TStore : ComponentStore<TState>
{

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    [Inject]
    protected IServiceProvider ServiceProvider { get; set; } = null!;

    private TStore _store = null!;
    /// <summary>
    /// Gets the component's store
    /// </summary>
    protected TStore Store
    {
        get
        {
            if (this._store != null) return this._store;
            this._store = ActivatorUtilities.CreateInstance<TStore>(this.ServiceProvider);
            return this._store;
        }
    }

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await this.Store.InitializeAsync();
    }

    private bool _Disposed;
    /// <summary>
    /// Disposes of the component
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the dispose of the component</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this._store.Dispose();
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

}
