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

using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace CloudStreams.Core.Api;

/// <summary>
/// Marks an endpoint that targets no namespace in particular and that filters its results by the namespaces the user may access (see #215)
/// </summary>
/// <remarks>
/// Such endpoints are open to users granted the required access level in any namespace, not only to those granted it on the whole cluster: they must
/// therefore only answer with what belongs to the namespaces the user may access
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class NamespaceFilteredAttribute
    : Attribute
{

}

/// <summary>
/// Represents the <see cref="IControllerModelConvention"/> that replaces the <c>[resources]</c> token of the routes of a <see cref="NamespacedResourceApiController{TResource}"/>
/// with the plural name of the resources it manages
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class NamespacedResourceRouteConvention
    : Attribute, IControllerModelConvention
{

    /// <inheritdoc/>
    public void Apply(ControllerModel controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        var resourceType = GetResourceType(controller.ControllerType);
        if (resourceType == null) return;
        var plural = ((IResource)Activator.CreateInstance(resourceType)!).Definition.Plural;
        foreach (var selector in controller.Actions.SelectMany(a => a.Selectors))
        {
            var route = selector.AttributeRouteModel;
            if (route?.Template == null || !route.Template.Contains(NamespacedResourceApiController<Resource>.ResourcesToken, StringComparison.Ordinal)) continue;
            route.Template = route.Template.Replace(NamespacedResourceApiController<Resource>.ResourcesToken, plural, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Gets the type of resources managed by the specified controller type
    /// </summary>
    /// <param name="controllerType">The controller type to get the type of managed resources of</param>
    /// <returns>The type of resources managed by the specified controller type, if any</returns>
    static Type? GetResourceType(Type? controllerType)
    {
        for (var type = controllerType; type != null && type != typeof(object); type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(NamespacedResourceApiController<>)) return type.GetGenericArguments()[0];
        }
        return null;
    }

}
