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

using CloudStreams.Broker.Application.Services;

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// An <see cref="IBrokerMetrics"/> that counts increments in memory
/// </summary>
/// <remarks>
/// <see cref="TotalDeliveryFailures"/> is the counter <see href="https://github.com/neuroglia-io/cloud-streams/issues/131">#131</see>
/// inflates: today it is incremented once per recursion level rather than once per real delivery attempt.
/// </remarks>
public class RecordingBrokerMetrics
    : IBrokerMetrics
{

    /// <summary>
    /// Gets the number of times <see cref="IncrementTotalPublishedEvents"/> was called
    /// </summary>
    public int TotalPublishedEvents { get; private set; }

    /// <summary>
    /// Gets the number of times <see cref="IncrementTotalDeliveryFailures"/> was called
    /// </summary>
    public int TotalDeliveryFailures { get; private set; }

    /// <inheritdoc/>
    public void IncrementTotalPublishedEvents(string? stream, string? subscriber) => this.TotalPublishedEvents++;

    /// <inheritdoc/>
    public void IncrementTotalDeliveryFailures(string? stream, string? subscriber) => this.TotalDeliveryFailures++;

    /// <inheritdoc/>
    public void Dispose() => GC.SuppressFinalize(this);

}
