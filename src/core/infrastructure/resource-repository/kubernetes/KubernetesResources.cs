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

using k8s;
using k8s.Models;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Exposes constants about Kubernetes resources required by Cloud Streams
/// </summary>
public static class KubernetesResources
{

    /// <summary>
    /// Exposes constants about Kubernetes Custom Resource Definitions required by Cloud Streams
    /// </summary>
    public static class ResourceDefinitions
    {

        /// <summary>
        /// Gets the <see cref="V1CustomResourceDefinition"/> for <see cref="Data.Models.Broker"/>s
        /// </summary>
        public static readonly V1CustomResourceDefinition Broker = LoadCustomResourceDefinition(nameof(Broker));
        /// <summary>
        /// Gets the <see cref="V1CustomResourceDefinition"/> for <see cref="Data.Models.Gateway"/>s
        /// </summary>
        public static readonly V1CustomResourceDefinition Gateway = LoadCustomResourceDefinition(nameof(Gateway));
        /// <summary>
        /// Gets the <see cref="V1CustomResourceDefinition"/> for <see cref="Data.Models.Broker"/>s
        /// </summary>
        public static readonly V1CustomResourceDefinition Network = LoadCustomResourceDefinition(nameof(Network));
        /// <summary>
        /// Gets the <see cref="V1CustomResourceDefinition"/> for <see cref="Data.Models.Subscription"/>s
        /// </summary>
        public static readonly V1CustomResourceDefinition Subscription = LoadCustomResourceDefinition(nameof(Subscription));

        /// <summary>
        /// Gets an <see cref="IEnumerable{T}"/> containing the <see cref="V1CustomResourceDefinition"/>s required by Cloud Streams
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<V1CustomResourceDefinition> AsEnumerable()
        {
            yield return Broker;
            yield return Gateway;
            yield return Network;
            yield return Subscription;
        }

        static V1CustomResourceDefinition LoadCustomResourceDefinition(string name)
        {
            var resourceName = string.Join('.', typeof(KubernetesResources).Namespace, "Assets", "ResourceDefinitions", $"{name.ToLowerInvariant()}.yaml");
            using var stream = typeof(KubernetesResources).Assembly.GetManifestResourceStream(resourceName)!;
            using var streamReader = new StreamReader(stream);
            var yaml = streamReader.ReadToEnd();
            return KubernetesYaml.Deserialize<V1CustomResourceDefinition>(yaml)!;
        }

    }


}