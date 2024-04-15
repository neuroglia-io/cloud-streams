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

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents a cloud event broker
/// </summary>
[DataContract]
public record Broker
    : Resource<BrokerSpec, BrokerStatus>
{

    /// <summary>
    /// Gets the <see cref="Broker"/>'s resource type
    /// </summary>
    public static readonly ResourceDefinitionInfo ResourceDefinition = new BrokerResourceDefinition()!;

    /// <inheritdoc/>
    public Broker() : base(ResourceDefinition) { }

    /// <inheritdoc/>
    public Broker(ResourceMetadata metadata, BrokerSpec spec, BrokerStatus? status = null) : base(ResourceDefinition, metadata, spec, status) { }

}
