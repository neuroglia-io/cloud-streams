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

using CloudStreams.Core.Resources;
using Neuroglia.Eventing.CloudEvents;
using Neuroglia.Serialization;
using System.Text.Json.Nodes;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Represents the state of a subscription, sampled at a given time
/// </summary>
/// <param name="At">The time the sample has been taken at</param>
/// <param name="Lag">The amount of cloud events the subscription had yet to handle, if known</param>
/// <param name="AckedOffset">The acknowledged offset of the subscription, if any</param>
public record SubscriptionSample(DateTimeOffset At, ulong? Lag, ulong? AckedOffset);

/// <summary>
/// Represents the acknowledgement rate of a subscription between two samples
/// </summary>
/// <param name="At">The time of the most recent of the two samples</param>
/// <param name="PerMinute">The amount of cloud events acknowledged per minute between the two samples</param>
public record SubscriptionAckRate(DateTimeOffset At, double PerMinute);

/// <summary>
/// Represents the samples of a subscription taken while its details are shown, which is what its lag and acknowledgement rate are charted from (see #224)
/// </summary>
/// <remarks>Neither the API nor the event store keeps a history of subscriptions: the history starts when the page is opened, and holds the most recent samples only</remarks>
/// <param name="capacity">The maximum amount of samples to keep</param>
public class SubscriptionSampleHistory(int capacity = 120)
{

    readonly List<SubscriptionSample> _samples = [];

    /// <summary>
    /// Gets the maximum amount of samples kept
    /// </summary>
    public int Capacity { get; } = capacity > 1 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));

    /// <summary>
    /// Gets the samples, oldest first
    /// </summary>
    public IReadOnlyList<SubscriptionSample> Samples => this._samples;

    /// <summary>
    /// Adds the specified sample, discarding the oldest one if the history is full
    /// </summary>
    /// <param name="sample">The sample to add</param>
    public void Add(SubscriptionSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        this._samples.Add(sample);
        if (this._samples.Count > this.Capacity) this._samples.RemoveAt(0);
    }

    /// <summary>
    /// Computes the acknowledgement rate between each pair of consecutive samples
    /// </summary>
    /// <returns>The acknowledgement rates, oldest first</returns>
    /// <remarks>
    /// A pair whose acknowledged offset is unknown, or went backwards because the subscription has been replayed or moved, has no meaningful rate, and is left out
    /// </remarks>
    public IReadOnlyList<SubscriptionAckRate> GetAckRates()
    {
        var rates = new List<SubscriptionAckRate>();
        for (var i = 1; i < this._samples.Count; i++)
        {
            var previous = this._samples[i - 1];
            var current = this._samples[i];
            var minutes = (current.At - previous.At).TotalMinutes;
            if (minutes <= 0 || !previous.AckedOffset.HasValue || !current.AckedOffset.HasValue || current.AckedOffset.Value < previous.AckedOffset.Value) continue;
            rates.Add(new(current.At, (current.AckedOffset.Value - previous.AckedOffset.Value) / minutes));
        }
        return rates;
    }

}

/// <summary>
/// Provides helpers to build the tests run by the subscription playground (see #224)
/// </summary>
public static class SubscriptionPlayground
{

    /// <summary>
    /// Formats the specified filter or mutation as YAML, to be edited
    /// </summary>
    /// <param name="value">The filter or mutation to format, if any</param>
    /// <param name="jsonSerializer">The service used to serialize JSON</param>
    /// <param name="yamlSerializer">The service used to serialize YAML</param>
    /// <returns>The YAML, or an empty string if there is nothing to format</returns>
    public static string ToYaml(object? value, IJsonSerializer jsonSerializer, IYamlSerializer yamlSerializer)
    {
        if (value == null) return string.Empty;
        return ResourceDocuments.ToYaml(jsonSerializer.SerializeToNode(value)!, yamlSerializer);
    }

    /// <summary>
    /// Builds the test to run
    /// </summary>
    /// <param name="partition">The partition to read the recent cloud events from, if any</param>
    /// <param name="filter">The filter to test, as YAML or JSON, if any</param>
    /// <param name="mutation">The mutation to test, as YAML or JSON, if any</param>
    /// <param name="samples">The sample cloud events to test, as a JSON array or object, or as a YAML stream, if any. If not set, recent cloud events are tested</param>
    /// <param name="length">The amount of recent cloud events to test</param>
    /// <param name="jsonSerializer">The service used to serialize JSON</param>
    /// <param name="yamlSerializer">The service used to serialize YAML</param>
    /// <returns>A new <see cref="SubscriptionTestRequest"/></returns>
    /// <exception cref="FormatException">The filter, the mutation or the samples are not valid</exception>
    public static SubscriptionTestRequest BuildRequest(PartitionReference? partition, string? filter, string? mutation, string? samples, int length, IJsonSerializer jsonSerializer, IYamlSerializer yamlSerializer)
    {
        var request = new SubscriptionTestRequest()
        {
            Partition = partition,
            Filter = Parse<CloudEventFilter>(filter, "filter", jsonSerializer, yamlSerializer),
            Mutation = Parse<CloudEventMutation>(mutation, "mutation", jsonSerializer, yamlSerializer)
        };
        if (string.IsNullOrWhiteSpace(samples)) request.Length = length;
        else
        {
            IReadOnlyList<JsonObject> documents;
            try { documents = ResourceDocuments.ParseDocuments(samples, yamlSerializer); }
            catch (FormatException ex) { throw new FormatException($"The sample cloud events are not valid: {ex.Message}", ex); }
            if (documents.Count == 0) throw new FormatException("There is no sample cloud event to test");
            if (documents.Count > SubscriptionTestRequest.MaxLength) throw new FormatException($"At most {SubscriptionTestRequest.MaxLength} sample cloud events may be tested");
            request.Events = [.. documents.Select((document, index) =>
            {
                CloudEvent? e;
                try { e = jsonSerializer.Deserialize<CloudEvent>(document.ToJsonString()); }
                catch (Exception ex) { throw new FormatException($"The sample cloud event #{index + 1} is not valid: {ex.Message}", ex); }
                if (e == null || string.IsNullOrWhiteSpace(e.Id) || string.IsNullOrWhiteSpace(e.Type) || e.Source == null) throw new FormatException($"The sample cloud event #{index + 1} must have an id, a source and a type");
                return e;
            })];
        }
        return request;
    }

    static T? Parse<T>(string? text, string name, IJsonSerializer jsonSerializer, IYamlSerializer yamlSerializer)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        IReadOnlyList<JsonObject> documents;
        try { documents = ResourceDocuments.ParseDocuments(text, yamlSerializer); }
        catch (FormatException ex) { throw new FormatException($"The {name} is not valid: {ex.Message}", ex); }
        if (documents.Count == 0) return null;
        if (documents.Count > 1) throw new FormatException($"The {name} must be a single object");
        try { return jsonSerializer.Deserialize<T>(documents[0].ToJsonString()); }
        catch (Exception ex) { throw new FormatException($"The {name} is not valid: {ex.Message}", ex); }
    }

}
