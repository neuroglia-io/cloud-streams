﻿// Copyright © 2024-Present The Cloud Streams Authors
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

using CloudStreams.Dashboard.Components.ResourceManagement;

namespace CloudStreams.Dashboard.Pages.Subscriptions.List;

/// <summary>
/// Represents the Subscription list view state
/// </summary>
public record SubscriptionListState
    : ResourceManagementComponentState<Subscription>
{
    /// <summary>
    /// Gets/sets the global stream size
    /// </summary>
    public ulong GlobalStreamLength { get; set; } = 0;

    /// <summary>
    /// Gets/sets the lengths of each partition
    /// </summary>
    public EquatableDictionary<string, ulong> PartitionLengths { get; set; } = [];
}
