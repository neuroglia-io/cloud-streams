using CloudStreams.Dashboard.StateManagement.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configures Flux services
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup Flux</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddFlux(this IServiceCollection services, Action<IFluxOptionsBuilder> setup)
    {
        var builder = new FluxOptionsBuilder(services);
        setup(builder);
        builder.Build();
        return services;
    }


    /// <summary>
    /// Adds and configures Flux services
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddFlux(this IServiceCollection services) => services.AddFlux(_ => { });

}
