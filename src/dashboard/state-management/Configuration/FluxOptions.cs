using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CloudStreams.Dashboard.StateManagement.Configuration;

/// <summary>
/// Represents the options used to configure Flux
/// </summary>
public class FluxOptions
{

    /// <summary>
    /// Gets/sets a <see cref="List{T}"/> containing the assemblies to scan for Flux components
    /// </summary>
    public virtual List<Assembly> AssembliesToScan { get; set; } = new();

    /// <summary>
    /// Gets/sets a boolean indicating whether or not to automatically register scanned <see cref="IFeature"/>s and <see cref="IReducer"/>s
    /// </summary>
    public virtual bool AutoRegisterFeatures { get; set; } = true;

    /// <summary>
    /// Gets/sets a boolean indicating whether or not to automatically register scanned <see cref="IEffect"/>s
    /// </summary>
    public virtual bool AutoRegisterEffects { get; set; } = true;

    /// <summary>
    /// Gets/sets a boolean indicating whether or not to automatically register scanned <see cref="IMiddleware"/>s
    /// </summary>
    public virtual bool AutoRegisterMiddlewares { get; set; } = false;

    /// <summary>
    /// Gets/sets the type of <see cref="IDispatcher"/> to use
    /// </summary>
    public virtual Type DispatcherType { get; set; } = typeof(Dispatcher);

    /// <summary>
    /// Gets/sets the type of <see cref="IStoreFactory"/> to use
    /// </summary>
    public virtual Type StoreFactoryType { get; set; } = typeof(StoreFactory);

    /// <summary>
    /// Gets/sets the type of <see cref="IStore"/> to use
    /// </summary>
    public virtual Type StoreType { get; set; } = typeof(Store);

    /// <summary>
    /// Gets/sets a <see cref="List{T}"/> containing the types of the <see cref="IFeature"/>s to use
    /// </summary>
    public virtual List<IFeature> Features { get; set; } = new();

    /// <summary>
    /// Gets/sets a <see cref="List{T}"/> containing the types of the <see cref="IMiddleware"/>s to use
    /// </summary>
    public virtual List<Type> Middlewares { get; set; } = new();

    /// <summary>
    /// Gets/sets a <see cref="List{T}"/> containing the types of the <see cref="IEffect"/>s to use
    /// </summary>
    public virtual List<IEffect> Effects { get; set; } = new();

    /// <summary>
    /// Gets/sets the lifetime of all Flux services
    /// </summary>
    public virtual ServiceLifetime ServiceLifetime { get; set; }

}
