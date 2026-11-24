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
/// Represents the options used to configure the leases brokers hold on the subscriptions they handle
/// </summary>
public class SubscriptionLeaseOptions
{

    /// <summary>
    /// Gets/sets a boolean indicating whether or not brokers must hold a lease on a subscription to handle it, which prevents replicas from handling the same
    /// subscription. Defaults to true
    /// </summary>
    public virtual bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets/sets the duration of a lease: how long a subscription stays unhandled when the broker that handles it dies. Defaults to 15 seconds
    /// </summary>
    public virtual TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets/sets the interval at which leases are renewed, and unhandled subscriptions acquired. Defaults to a third of the <see cref="Duration"/>
    /// </summary>
    public virtual TimeSpan? RenewalInterval { get; set; }

    /// <summary>
    /// Gets the effective interval at which leases are renewed
    /// </summary>
    /// <returns>The effective interval at which leases are renewed</returns>
    public virtual TimeSpan GetRenewalInterval() => this.RenewalInterval is { } interval && interval > TimeSpan.Zero && interval < this.Duration ? interval : this.Duration / 3;

}
