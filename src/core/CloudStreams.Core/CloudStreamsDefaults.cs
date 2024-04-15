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

using CloudStreams.Core.Resources;
using System.Diagnostics;

namespace CloudStreams.Core;

/// <summary>
/// Exposes defaults about Cloud Streams
/// </summary>
public static class CloudStreamsDefaults
{

    /// <summary>
    /// Gets the default group for Cloud Streams resources
    /// </summary>
    public const string ResourceGroup = "cloud-streams.io";

    /// <summary>
    /// Exposes Cloud Streams default resources
    /// </summary>
    public static class Resources
    {

        /// <summary>
        /// Exposes Cloud Streams resource definitions
        /// </summary>
        public static class Definitions
        {

            /// <summary>
            /// Gets the definition of Broker resources
            /// </summary>
            public static ResourceDefinition Broker { get; } = new BrokerResourceDefinition();

            /// <summary>
            /// Gets the definition of Gateway resources
            /// </summary>
            public static ResourceDefinition Gateway { get; } = new GatewayResourceDefinition();

            /// <summary>
            /// Gets the definition of Subscription resources
            /// </summary>
            public static ResourceDefinition Subscription { get; } = new SubscriptionResourceDefinition();

            /// <summary>
            /// Gets a new <see cref="IEnumerable{T}"/> containing Cloud Streams default resource definitions
            /// </summary>
            /// <returns></returns>
            public static IEnumerable<ResourceDefinition> AsEnumerable()
            {
                yield return Broker;
                yield return Gateway;
                yield return Subscription;
            }

        }

    }

    /// <summary>
    /// Exposes constants about Cloud Streams application telemetry
    /// </summary>
    public static class Telemetry
    {

        /// <summary>
        /// Exposes the Cloud Streams application's <see cref="System.Diagnostics.ActivitySource"/>
        /// </summary>
        public static ActivitySource ActivitySource { get; set; } = null!;

    }

}
