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
            public static ResourceDefinition Broker { get; } = LoadResourceDefinition(nameof(Broker));

            /// <summary>
            /// Gets the definition of Gateway resources
            /// </summary>
            public static ResourceDefinition Gateway { get; } = LoadResourceDefinition(nameof(Gateway));

            /// <summary>
            /// Gets the definition of Subscription resources
            /// </summary>
            public static ResourceDefinition Subscription { get; } = LoadResourceDefinition(nameof(Subscription));

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

            static ResourceDefinition LoadResourceDefinition(string name)
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Definitions", $"{name.ToHyphenCase()}.yaml");
                var yaml = File.ReadAllText(filePath);
                var resourceDefinition = Hylo.Serializer.Json.Deserialize<ResourceDefinition>(Hylo.Serializer.Json.Serialize(Hylo.Serializer.Yaml.Deserialize<IDictionary<string, object>>(yaml)!))!;
                return resourceDefinition;
            }

        }

    }

}
