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

namespace CloudStreams.Dashboard.Components.Help;

/// <summary>
/// Represents a topic of the dashboard's help: a concept of Cloud Streams, which a section of the Help page explains, and the contextual help of its pages summarizes
/// (see #271)
/// </summary>
/// <param name="Id">The topic's id, which is the anchor of its section on the Help page</param>
/// <param name="Title">The topic's title</param>
/// <param name="Summary">Two or three sentences that summarize the topic, shown by the contextual help and at the top of its section</param>
public sealed record HelpTopic(string Id, string Title, string Summary)
{

    /// <summary>
    /// Gets the address of the topic's section on the Help page
    /// </summary>
    public string Href => $"help#{this.Id}";

}

/// <summary>
/// Defines the topics of the dashboard's help, in the order of the Help page
/// </summary>
public static class HelpTopicCatalog
{

    /// <summary>
    /// Gets the topic that explains how the concepts of Cloud Streams fit together
    /// </summary>
    public static readonly HelpTopic Overview = new("overview", "How it fits together",
        "Publishers send cloud events to a gateway, which admits them and records them, in order, in the stream. Brokers read the stream, and deliver its events to the subscribers of subscriptions.");

    /// <summary>
    /// Gets the topic that explains cloud events and the stream
    /// </summary>
    public static readonly HelpTopic Events = new("events", "Events and the stream",
        "Every event a gateway admits is appended to the stream, an ordered log where it gets an offset. Events are never changed: they are read, searched and delivered from the stream, and expire according to its retention policy.");

    /// <summary>
    /// Gets the topic that explains partitions
    /// </summary>
    public static readonly HelpTopic Partitions = new("partitions", "Partitions",
        "Events are indexed by their source, type, subject, correlation id and causation id. A partition is the ordered subset of the stream that shares one of these values, which a subscription can consume instead of the whole stream.");

    /// <summary>
    /// Gets the topic that explains gateways
    /// </summary>
    public static readonly HelpTopic Gateways = new("gateways", "Gateways",
        "A gateway receives cloud events, over HTTP or from feeds, and decides which ones are admitted: who may publish what, whether their data matches their schema, and which metadata is extracted from them. Admitted events are recorded in the stream of its namespace. On Kubernetes, the operator may deploy it from its resource: its Workload column then tells whether its replicas are ready.");

    /// <summary>
    /// Gets the topic that explains brokers
    /// </summary>
    public static readonly HelpTopic Brokers = new("brokers", "Brokers",
        "A broker delivers the events of the stream to the subscribers of the subscriptions of its namespace. It retries failed deliveries, and records how far each subscription got. On Kubernetes, the operator may deploy it from its resource: its Workload column then tells whether its replicas are ready.");

    /// <summary>
    /// Gets the topic that explains subscriptions
    /// </summary>
    public static readonly HelpTopic Subscriptions = new("subscriptions", "Subscriptions",
        "A subscription tells a broker which events to deliver, where, and how: the whole stream or a partition, an optional filter and mutation, and a subscriber, which is an endpoint or a sink.");

    /// <summary>
    /// Gets the topic that explains offsets and acknowledgements
    /// </summary>
    public static readonly HelpTopic Delivery = new("delivery", "Offsets and acknowledgements",
        "A subscription that tracks an offset receives its events in order, at least once, and remembers the offset of the next event to deliver. A subscriber acknowledges an event by responding with a 2xx status, or later, by calling back after responding 202 Accepted.");

    /// <summary>
    /// Gets the topic that explains faults and how to recover from them
    /// </summary>
    public static readonly HelpTopic Faults = new("faults", "Faults and recovery",
        "When an event cannot be delivered, even after retrying, the subscription is faulted and stops at that event. Resuming retries the event, skipping moves past it, suspending pauses delivery, and replaying delivers every event again.");

    /// <summary>
    /// Gets the topic that explains sinks
    /// </summary>
    public static readonly HelpTopic Sinks = new("sinks", "Sinks",
        "A sink is a named endpoint that events are delivered to, such as a webhook, a Kafka topic or a NATS subject, with how to authenticate to it. Subscriptions refer to it by name, and cannot read its credentials.");

    /// <summary>
    /// Gets the topic that explains feeds
    /// </summary>
    public static readonly HelpTopic Feeds = new("feeds", "Feeds",
        "A feed is a named endpoint that gateways consume events from, such as a Kafka topic, a NATS subject or an AMQP queue, with how to authenticate to it. The events of a feed are admitted like the events published to the gateway.");

    /// <summary>
    /// Gets the topic that explains schemas
    /// </summary>
    public static readonly HelpTopic Schemas = new("schemas", "Schemas",
        "A schema is a versioned JSON schema, which gateways validate the data of events against. Its compatibility mode decides which new versions may be registered. Schemas are shared by every namespace.");

    /// <summary>
    /// Gets the topic that explains namespaces
    /// </summary>
    public static readonly HelpTopic Namespaces = new("namespaces", "Namespaces",
        "A namespace isolates resources and events: its gateways, brokers, subscriptions, sinks and feeds only work with each other, its events are only delivered within it, and users may be granted access to it alone.");

    /// <summary>
    /// Gets the topic that explains the playground
    /// </summary>
    public static readonly HelpTopic Playground = new("playground", "Playground",
        "The playground tries a filter and a mutation against recent or sample events, and shows which events would be delivered, and how. Nothing is saved, and nothing is delivered.");

    /// <summary>
    /// Gets every topic, in the order of the Help page
    /// </summary>
    public static IReadOnlyList<HelpTopic> All { get; } = [Overview, Events, Partitions, Gateways, Brokers, Subscriptions, Delivery, Faults, Sinks, Feeds, Schemas, Namespaces, Playground];

}
