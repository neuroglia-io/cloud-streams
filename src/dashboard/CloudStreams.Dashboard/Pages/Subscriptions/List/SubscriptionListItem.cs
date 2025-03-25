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

namespace CloudStreams.Dashboard.Pages.Subscriptions.List;

/// <summary>
/// Represents a list item for a <see cref="Subscription"/>
/// </summary>
public record SubscriptionListItem
    : Subscription
{

    /// <inheritdoc/>
    public SubscriptionListItem() : base() { }

    /// <inheritdoc/>
    public SubscriptionListItem(ResourceMetadata metadata, SubscriptionSpec spec, SubscriptionStatus? status = null, ulong length = 0) 
        : base(metadata, spec, status)
    {
        this.StreamLength = length;
    }

    /// <summary>
    /// Creates a new <see cref="SubscriptionListItem"/> from a <see cref="Subscription"/>
    /// </summary>
    ///<param name="subscription">The <see cref="Subscription"/> to create the new <see cref="SubscriptionListItem"/> with</param>
    /// <param name="length">The length of the stream the subscription subscribes to</param>
    public SubscriptionListItem(Subscription subscription, ulong length = 0)
        : base(subscription.Metadata, subscription.Spec, subscription.Status)
    {
        this.StreamLength = length;
    }

    /// <summary>
    /// Gets/sets the length of the stream the subscription is based on
    /// </summary>
    public ulong StreamLength { get; set; }
}
