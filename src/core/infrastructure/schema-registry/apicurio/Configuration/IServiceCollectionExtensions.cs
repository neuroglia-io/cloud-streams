﻿using CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Configuration;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configures a new Api Curio Registry client
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup <see cref="ApiCurioRegistryClientOptions"/></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static IServiceCollection AddApiCurioRegistryClient(this IServiceCollection services, Action<ApiCurioRegistryClientOptions>? setup = null, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        var options = new ApiCurioRegistryClientOptions();
        if (setup != null) setup(options);
        services.AddSingleton(Options.Create(options));
        services.AddHttpClient(typeof(ApicurioRegistryApiClient).Name, httpClient =>
        {
            httpClient.BaseAddress = options.ServerUri;
        });
        services.Add(new(typeof(ApicurioRegistryApiClient), typeof(ApicurioRegistryApiClient), lifetime));
        services.Add(new(typeof(IApicurioRegistryApiClient), provider => provider.GetRequiredService<ApicurioRegistryApiClient>(), lifetime));
        return services;
    }

}