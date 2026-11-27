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

using CloudStreams.Core.Api.Configuration;
using k8s;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Creates the configuration of the clients of the Kubernetes API that the Kubernetes store of resources uses (see #275)
/// </summary>
public static class KubernetesClientConfigurationFactory
{

    /// <summary>
    /// Creates the configuration of the client of the Kubernetes API: the pod's service account in a cluster, or a kubeconfig file outside of one
    /// </summary>
    /// <param name="options">The options of the Kubernetes store</param>
    /// <returns>A new <see cref="KubernetesClientConfiguration"/></returns>
    public static KubernetesClientConfiguration Create(KubernetesResourceStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.KubeConfigPath) && string.IsNullOrWhiteSpace(options.Context) && KubernetesClientConfiguration.IsInCluster()) return KubernetesClientConfiguration.InClusterConfig();
        return KubernetesClientConfiguration.BuildConfigFromConfigFile(string.IsNullOrWhiteSpace(options.KubeConfigPath) ? null : options.KubeConfigPath, string.IsNullOrWhiteSpace(options.Context) ? null : options.Context);
    }

}
