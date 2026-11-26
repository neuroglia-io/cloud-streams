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

namespace CloudStreams.Gateway.Application.Configuration;

/// <summary>
/// Represents the options used to configure a Cloud Streams cloud event gateway
/// </summary>
/// <remarks>
/// A gateway application serves a single <see cref="Core.Resources.Gateway"/> resource, which it creates when missing. The cloud events it ingests belong to the
/// namespace of that resource (see #215)
/// </remarks>
public class GatewayOptions
{

    /// <summary>
    /// Gets the prefix for all Cloud Streams gateway related environment variables
    /// </summary>
    public const string EnvironmentVariablePrefix = "CLOUDSTREAMS_GATEWAY_";

    /// <summary>
    /// Gets the name of the gateway when none is configured
    /// </summary>
    public const string DefaultName = "default";

    /// <summary>
    /// Gets/sets the name of the gateway resource the application serves. Defaults to <c>default</c>
    /// </summary>
    public virtual string? Name { get; set; }

    /// <summary>
    /// Gets/sets the namespace of the gateway resource the application serves, which the cloud events it ingests belong to. Defaults to the default namespace
    /// </summary>
    public virtual string? Namespace { get; set; }

    /// <summary>
    /// Gets the name of the gateway resource the application serves
    /// </summary>
    /// <returns>The name of the gateway resource</returns>
    public virtual string GetName() => string.IsNullOrWhiteSpace(this.Name) ? DefaultName : this.Name.Trim();

    /// <summary>
    /// Gets the namespace of the gateway resource the application serves
    /// </summary>
    /// <returns>The namespace of the gateway resource</returns>
    public virtual string GetNamespace() => string.IsNullOrWhiteSpace(this.Namespace) ? Neuroglia.Data.Infrastructure.ResourceOriented.Namespace.DefaultNamespaceName : this.Namespace.Trim();

}
