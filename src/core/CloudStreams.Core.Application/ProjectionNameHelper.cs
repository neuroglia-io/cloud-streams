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

namespace CloudStreams;

/// <summary>
/// Exposes the names of the EventStore projections used by Cloud Streams
/// </summary>
/// <remarks>
/// The partitions of earlier versions, named <c>cloud_events_by_source</c>, <c>cloud_events_by_subject</c> and <c>cloud_events_by_causation_id</c>, spanned every
/// namespace: they are no longer created, and may be deleted once upgraded (see #215)
/// </remarks>
public static class ProjectionNameHelper
{

    /// <summary>
    /// Gets the name of the projection used to link <see cref="CloudEvent"/>s to the stream of their namespace
    /// </summary>
    public const string PartitionByNamespace = "cloud_events_by_namespace";

    /// <summary>
    /// Gets the name of the projection used to partition the <see cref="CloudEvent"/>s of each namespace by source
    /// </summary>
    public const string PartitionBySource = "cloud_events_by_namespaced_source";

    /// <summary>
    /// Gets the name of the projection used to partition the <see cref="CloudEvent"/>s of each namespace by subject
    /// </summary>
    public const string PartitionBySubject = "cloud_events_by_namespaced_subject";

    /// <summary>
    /// Gets the name of the projection used to partition the <see cref="CloudEvent"/>s of each namespace by type
    /// </summary>
    public const string PartitionByType = "cloud_events_by_namespaced_type";

    /// <summary>
    /// Gets the name of the projection used to partition the <see cref="CloudEvent"/>s of each namespace by $correlationId
    /// </summary>
    public const string PartitionByCorrelationId = "cloud_events_by_namespaced_correlation_id";

    /// <summary>
    /// Gets the name of the projection used to partition the <see cref="CloudEvent"/>s of each namespace by $causationId
    /// </summary>
    public const string PartitionByCausationId = "cloud_events_by_namespaced_causation_id";

    /// <summary>
    /// Gets the prefix of name of the projection used to gather all <see cref="CloudEvent"/>'s partitions ids
    /// </summary>
    public const string CloudEventPartitionsMetadataPrefix = "cloud_events_partition_";

}
