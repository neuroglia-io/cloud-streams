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

using CloudStreams.Core.Data.Models;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Exposes constants about the EventStore projections used by Cloud Streams
/// </summary>
public static class EventStoreProjections
{

    /// <summary>
    /// Gets the name of the projection used to partition <see cref="CloudEvent"/>s by source
    /// </summary>
    public const string PartitionBySource = "cloud_events_by_source";
    /// <summary>
    /// Gets the prefix of name of the projection used to gather all <see cref="CloudEvent"/>'s partitions ids
    /// </summary>
    public const string CloudEventPartitionsMetadataPrefix = "cloud_events_partition_";

    /// <summary>
    /// Exposes constants about EventStore built-in projections
    /// </summary>
    public static class BuiltInProjections
    {

        /// <summary>
        /// Gets the name of the built-in projection used to partition events by category
        /// </summary>
        public const string PartitionByCategory = "$by_category";
        /// <summary>
        /// Gets the name of the built-in projection used to partition events by type
        /// </summary>
        public const string PartitionByEventType = "$by_event_type";
        /// <summary>
        /// Gets the name of the built-in projection used to partition events by correlation id
        /// </summary>
        public const string PartitionByCorrelationId = "$by_correlation_id";
        /// <summary>
        /// Gets the name of the built-in projection used to collect stream-related events
        /// </summary>
        public const string Streams = "$streams";
        /// <summary>
        /// Gets the name of the built-in projection used to collect stream-related events by category
        /// </summary>
        public const string StreamByCategory = "$stream_by_category";

    }

}
