using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Reflection;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="IEndpointRouteBuilder"/>s
/// </summary>
public static class IEndpointRouteBuilderExtensions
{

    private static readonly MethodInfo MapHubMethod = typeof(HubEndpointRouteBuilderExtensions).GetMethods(BindingFlags.Default | BindingFlags.Public | BindingFlags.Static).Single(m => m.Name == nameof(HubEndpointRouteBuilderExtensions.MapHub) && m.GetParameters().Length == 2);

    /// <summary>
    /// Maps all <see cref="Hub"/>s found in loaded assemblies
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointRouteBuilder"/> to configure</param>
    /// <returns>The configured <see cref="IEndpointRouteBuilder"/></returns>
    public static IEndpointRouteBuilder MapHubs(this IEndpointRouteBuilder builder)
    {
        foreach(var type in TypeCacheUtil.FindFilteredTypes("cs:hubs", t => t.IsClass && !t.IsAbstract && !t.IsInterface && typeof(Hub).IsAssignableFrom(t)))
        {
            if (!type.TryGetCustomAttribute<RouteAttribute>(out var routeAttribute) || routeAttribute == null) continue;
            MapHubMethod.MakeGenericMethod(type).Invoke(null, new object[] { builder, routeAttribute.Template });
        }
        return builder;
    }

}