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

namespace CloudStreams.Core.Application.Configuration;

/// <summary>
/// Represents the options used to restrict the destinations of the requests Cloud Streams sends on behalf of resources: deliveries to subscribers, mutation webhooks
/// and health checks
/// </summary>
/// <remarks>
/// Those destinations are configured by whoever can manage resources. These options keep them from reaching addresses they should not, such as the cloud provider's
/// metadata service (see #148). Subscribers are usually internal services, so private networks are allowed by default
/// </remarks>
public class OutboundRequestOptions
{

    /// <summary>
    /// Gets the name of the configuration section the options are bound to
    /// </summary>
    public const string ConfigurationSection = "OutboundRequests";

    /// <summary>
    /// Gets/sets the hosts requests may be sent to. Entries are host names (e.g. <c>subscriber.example.com</c>) or wildcards (e.g. <c>*.svc.cluster.local</c>).
    /// If empty, requests may be sent to any host whose addresses are not denied
    /// </summary>
    public virtual List<string> AllowedHosts { get; set; } = [];

    /// <summary>
    /// Gets/sets the networks, in CIDR notation (e.g. <c>10.0.0.0/8</c>), requests may not be sent to
    /// </summary>
    public virtual List<string> DeniedNetworks { get; set; } = [];

    /// <summary>
    /// Gets/sets a boolean indicating whether or not requests to link-local addresses, which include the metadata services of cloud providers (e.g. 169.254.169.254),
    /// are denied. Defaults to true
    /// </summary>
    public virtual bool DenyLinkLocal { get; set; } = true;

    /// <summary>
    /// Gets/sets a boolean indicating whether or not requests to private, loopback and link-local addresses are denied. Defaults to false
    /// </summary>
    public virtual bool DenyPrivateNetworks { get; set; }

}
