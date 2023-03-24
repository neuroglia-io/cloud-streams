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

namespace CloudStreams.Dashboard.Components.ReadOptionsFormStateManagement;

/// <summary>
/// Represents the state of the form used to manipulate <see cref="StreamReadOptions"/>
/// </summary>
public record ReadOptionsFormState
{
    /// <summary>
    /// Gets/sets a reference to the cloud event partition's type to read, if any
    /// </summary>
    public CloudEventPartitionType? PartitionType { get; set; } = null;
    /// <summary>
    /// Gets/sets a reference to the cloud event partition's id to read, if any
    /// </summary>
    public string? PartitionId { get; set; } = null;

    /// <summary>
    /// Gets/sets the direction in which to read the stream of cloud events
    /// </summary>
    public StreamReadDirection Direction { get; set; } = StreamReadDirection.Backwards;

    /// <summary>
    /// Gets/sets the offset starting from which to read the stream
    /// </summary>
    public long? Offset { get; set; } = null;

    /// <summary>
    /// Gets/sets the amount of events to read from the stream
    /// </summary>
    public ulong? Length { get; set; } = null;

    /// <summary>
    /// Gets/sets the total amount of events in the stream
    /// </summary>
    public ulong? StreamLength { get; set; } = null;

    /// <summary>
    /// Gets the <see cref="List{T}"/> of suggested <see cref="PartitionReference"/>s
    /// </summary>
    public List<string>? Partitions { get; set; } = new();
}
