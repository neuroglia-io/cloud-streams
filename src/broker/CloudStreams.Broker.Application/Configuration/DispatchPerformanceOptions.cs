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

namespace CloudStreams.Broker.Application.Configuration;

/// <summary>
/// Represents the options used to configure how a broker catches up the records a subscription has missed
/// </summary>
public class CatchUpOptions
{

    /// <summary>
    /// Gets the default amount of records read at once during catch-up
    /// </summary>
    public const int DefaultPageSize = 100;

    /// <summary>
    /// Gets/sets the amount of records read at once during catch-up. The next page is read while the current one is being delivered. Defaults to 100
    /// </summary>
    public virtual int PageSize { get; set; } = DefaultPageSize;

    /// <summary>
    /// Gets the amount of records to read at once, falling back to <see cref="DefaultPageSize"/> when the configured value is not strictly positive
    /// </summary>
    /// <returns>The amount of records to read at once</returns>
    public virtual int GetPageSize() => this.PageSize > 0 ? this.PageSize : DefaultPageSize;

}

/// <summary>
/// Represents the options used to configure how a broker commits the offsets of the events it has delivered
/// </summary>
/// <remarks>
/// Committing an offset writes the subscription's status. By default, every delivery is committed at once. Batching commits saves writes, at the cost of
/// redelivering the events delivered since the last commit if the broker dies: delivery remains at-least-once. Committed offsets are always written before a
/// delivery is suspended for a deferred acknowledgement, before the subscription is faulted, and when the handler stops
/// </remarks>
public class OffsetCommitOptions
{

    /// <summary>
    /// Gets/sets the maximum amount of deliveries whose offset may remain uncommitted. Defaults to 1, which commits every delivery at once
    /// </summary>
    public virtual int BatchSize { get; set; } = 1;

    /// <summary>
    /// Gets/sets the maximum amount of time the offset of a delivery may remain uncommitted, when <see cref="BatchSize"/> is greater than 1. Defaults to 1 second
    /// </summary>
    public virtual TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets a boolean indicating whether or not commits are batched
    /// </summary>
    public virtual bool IsBatched => this.BatchSize > 1 && this.Interval > TimeSpan.Zero;

}
