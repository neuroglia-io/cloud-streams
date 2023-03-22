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