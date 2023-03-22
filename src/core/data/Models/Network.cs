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

namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents a cloud event network
/// </summary>
[DataContract]
public class Network
    : Resource<NetworkSpec, NetworkStatus>
{

    const string ResourceGroup = CloudStreamsDefaults.ResourceGroup;

    const string ResourceVersion = "v1";

    const string ResourcePlural = "networks";

    const string ResourceKind = "Network";

    /// <summary>
    /// Gets the <see cref="Network"/>'s resource type
    /// </summary>
    public static readonly ResourceType ResourceType = new(ResourceGroup, ResourceVersion, ResourcePlural, ResourceKind);

    /// <inheritdoc/>
    public Network() : base(ResourceType) { }

    /// <inheritdoc/>
    public Network(ResourceMetadata metadata, NetworkSpec spec, NetworkStatus? status = null) : base(ResourceType, metadata, spec, status) { }

}
