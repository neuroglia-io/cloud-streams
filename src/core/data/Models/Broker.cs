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
/// Represents a cloud event broker
/// </summary>
[DataContract]
public class Broker
    : Resource<BrokerSpec, BrokerStatus>
{

    const string ResourceGroup = CloudStreamsDefaults.ResourceGroup;

    const string ResourceVersion = "v1";

    const string ResourcePlural = "brokers";

    const string ResourceKind = "Broker";

    /// <summary>
    /// Gets the <see cref="Broker"/>'s resource type
    /// </summary>
    public static readonly ResourceType ResourceType = new(ResourceGroup, ResourceVersion, ResourcePlural, ResourceKind);

    /// <inheritdoc/>
    public Broker() : base(ResourceType) { }

    /// <inheritdoc/>
    public Broker(ResourceMetadata metadata, BrokerSpec spec, BrokerStatus? status = null) : base(ResourceType, metadata, spec, status) { }

}
