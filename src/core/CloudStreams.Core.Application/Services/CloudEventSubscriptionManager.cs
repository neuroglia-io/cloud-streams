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

using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents the service used to manage Cloud Streams subscriptions through the CloudEvents Subscriptions API, which it maps onto them (see #219)
/// </summary>
/// <param name="resources">The service used to manage resources</param>
/// <param name="serializer">The service used to serialize the subscriptions created through the CloudEvents Subscriptions API</param>
/// <remarks>
/// <para>
/// A CloudEvents subscription is stored as a Cloud Streams subscription: its sink is the subscriber, its source, types and filters are compiled into a filter, and
/// it tracks an offset, starting at the end of the stream unless its <c>config</c> sets one. The subscription as it was submitted is kept in an annotation, so that
/// it is returned as is. Cloud Streams subscriptions created otherwise are described as well as they can be.
/// </para>
/// <para>
/// Filters made only of <c>exact</c>, <c>prefix</c> and <c>suffix</c> expressions, on different attributes, are compiled into attribute filters; filters that use
/// <c>all</c>, <c>any</c> or <c>not</c> otherwise, into a runtime expression. The optional <c>sql</c> dialect is not supported.
/// </para>
/// </remarks>
public class CloudEventSubscriptionManager(IResourceRepository resources, IJsonSerializer serializer)
{

    /// <summary>
    /// Gets the name of the annotation that holds the CloudEvents subscription a Cloud Streams subscription has been created from
    /// </summary>
    public const string AnnotationName = "cloudevents.io/subscription";

    /// <summary>
    /// Gets the name of the configuration key that sets the offset starting from which to deliver cloud events
    /// </summary>
    public const string OffsetConfigKey = "offset";

    /// <summary>
    /// Gets the type of the problem that describes an invalid or unsupported CloudEvents subscription
    /// </summary>
    public static readonly Uri InvalidSubscriptionProblemType = new("https://cloud-streams.io/problems/invalid-subscription");

    /// <summary>
    /// Lists all subscriptions
    /// </summary>
    /// <param name="namespace">The namespace the subscriptions belong to. Defaults to the default namespace</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>All subscriptions, described as CloudEvents subscriptions</returns>
    public virtual async Task<IReadOnlyList<CloudEventSubscription>> ListAsync(string? @namespace = null, CancellationToken cancellationToken = default)
    {
        var subscriptions = new List<CloudEventSubscription>();
        await foreach (var subscription in resources.GetAllAsync<Subscription>(Resolve(@namespace), null, cancellationToken).ConfigureAwait(false)) subscriptions.Add(this.Describe(subscription));
        return subscriptions;
    }

    /// <summary>
    /// Gets the specified subscription
    /// </summary>
    /// <param name="id">The id of the subscription to get</param>
    /// <param name="namespace">The namespace the subscriptions belong to. Defaults to the default namespace</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The specified subscription, or null if it does not exist</returns>
    public virtual async Task<CloudEventSubscription?> GetAsync(string id, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        var subscription = await this.FindAsync(id, @namespace, cancellationToken).ConfigureAwait(false);
        return subscription == null ? null : this.Describe(subscription);
    }

    /// <summary>
    /// Creates a new subscription. The id of the specified subscription, if any, is ignored
    /// </summary>
    /// <param name="subscription">The subscription to create</param>
    /// <param name="namespace">The namespace the subscriptions belong to. Defaults to the default namespace</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The subscription created</returns>
    /// <exception cref="ProblemDetailsException">The subscription is invalid, or unsupported</exception>
    public virtual async Task<CloudEventSubscription> CreateAsync(CloudEventSubscription subscription, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        var resource = new Subscription()
        {
            Metadata = new() { Name = $"ce-{Guid.NewGuid():N}"[..19], Namespace = Resolve(@namespace) },
            Spec = new() { Subscriber = new() }
        };
        this.Apply(subscription, resource);
        resource = await resources.AddAsync(resource, false, cancellationToken).ConfigureAwait(false);
        return this.Describe(resource);
    }

    /// <summary>
    /// Updates the specified subscription. Its Cloud Streams specific settings, such as its partition or its retry policy, are kept
    /// </summary>
    /// <param name="id">The id of the subscription to update</param>
    /// <param name="namespace">The namespace the subscriptions belong to. Defaults to the default namespace</param>
    /// <param name="subscription">The subscription to update it with</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The updated subscription, or null if it does not exist</returns>
    /// <exception cref="ProblemDetailsException">The subscription is invalid, or unsupported</exception>
    public virtual async Task<CloudEventSubscription?> UpdateAsync(string id, CloudEventSubscription subscription, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (subscription.Id != null && subscription.Id != id) throw Invalid($"The id of the subscription, '{subscription.Id}', differs from the one of the subscription to update, '{id}'");
        var resource = await this.FindAsync(id, @namespace, cancellationToken).ConfigureAwait(false);
        if (resource == null) return null;
        this.Apply(subscription, resource);
        resource = await resources.ReplaceAsync(resource, false, cancellationToken).ConfigureAwait(false);
        return this.Describe(resource);
    }

    /// <summary>
    /// Deletes the specified subscription
    /// </summary>
    /// <param name="id">The id of the subscription to delete</param>
    /// <param name="namespace">The namespace the subscriptions belong to. Defaults to the default namespace</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The deleted subscription, or null if it does not exist</returns>
    public virtual async Task<CloudEventSubscription?> DeleteAsync(string id, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        var resource = await this.FindAsync(id, @namespace, cancellationToken).ConfigureAwait(false);
        if (resource == null) return null;
        await resources.RemoveAsync<Subscription>(resource.GetName(), resource.GetNamespace(), false, cancellationToken).ConfigureAwait(false);
        return this.Describe(resource);
    }

    /// <summary>
    /// Finds the specified subscription
    /// </summary>
    /// <param name="id">The id of the subscription to find</param>
    /// <param name="namespace">The namespace the subscriptions belong to. Defaults to the default namespace</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The subscription, or null if it does not exist</returns>
    protected virtual async Task<Subscription?> FindAsync(string id, string? @namespace, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        try { return await resources.GetAsync<Subscription>(id, Resolve(@namespace), cancellationToken).ConfigureAwait(false); }
        catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { return null; }
    }

    static string Resolve(string? @namespace) => string.IsNullOrWhiteSpace(@namespace) ? Namespace.DefaultNamespaceName : @namespace;

    /// <summary>
    /// Applies the specified CloudEvents subscription to the specified Cloud Streams subscription
    /// </summary>
    /// <param name="subscription">The CloudEvents subscription to apply</param>
    /// <param name="resource">The Cloud Streams subscription to apply it to</param>
    /// <exception cref="ProblemDetailsException">The subscription is invalid, or unsupported</exception>
    protected virtual void Apply(CloudEventSubscription subscription, Subscription resource)
    {
        if (subscription.Protocol == null) throw Invalid("'protocol' is required");
        if (subscription.Protocol != CloudEventSubscription.HttpProtocol) throw Invalid($"The protocol '{subscription.Protocol}' is not supported: Cloud Streams only delivers cloud events with '{CloudEventSubscription.HttpProtocol}'");
        if (string.IsNullOrWhiteSpace(subscription.Sink) || !Uri.TryCreate(subscription.Sink, UriKind.Absolute, out var sink) || (sink.Scheme != Uri.UriSchemeHttp && sink.Scheme != Uri.UriSchemeHttps)) throw Invalid("'sink' is required, and must be an absolute HTTP or HTTPS URI");
        if (subscription.SinkCredential != null) throw Invalid("'sinkcredential' is not supported");
        if (subscription.ProtocolSettings != null)
        {
            foreach (var (name, value) in subscription.ProtocolSettings)
            {
                if (name == "method" && value is JsonValue method && method.TryGetValue<string>(out var verb) && string.Equals(verb, HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase)) continue;
                throw Invalid(name == "method" ? "Cloud Streams only delivers cloud events with the 'POST' method" : $"The protocol setting '{name}' is not supported");
            }
        }
        long? offset = null;
        if (subscription.Config != null)
        {
            foreach (var (name, value) in subscription.Config)
            {
                if (name != OffsetConfigKey) throw Invalid($"The configuration key '{name}' is not supported. Supported keys: '{OffsetConfigKey}'");
                if (value is not JsonValue offsetValue || !offsetValue.TryGetValue<long>(out var parsed) || parsed < StreamPosition.EndOfStream) throw Invalid($"'config.{OffsetConfigKey}' must be an integer: 0 starts at the beginning of the stream, -1 at its end");
                offset = parsed;
            }
        }
        var filter = CloudEventSubscriptionFilter.Compile(subscription);
        var submitted = subscription with { Id = null, SinkCredential = null };
        resource.Spec.Subscriber.Endpoint = sink;
        resource.Spec.Subscriber.Sink = null;
        resource.Spec.Filter = filter;
        if (offset.HasValue || resource.Spec.Stream == null) resource.Spec.Stream = new() { Offset = offset ?? StreamPosition.EndOfStream };
        resource.Metadata.Annotations ??= new Dictionary<string, string>();
        resource.Metadata.Annotations[AnnotationName] = serializer.SerializeToText(submitted);
    }

    /// <summary>
    /// Describes the specified Cloud Streams subscription as a CloudEvents subscription
    /// </summary>
    /// <param name="resource">The subscription to describe</param>
    /// <returns>The subscription as it was submitted, if it has been created through the CloudEvents Subscriptions API, or as well as it can be described otherwise</returns>
    public virtual CloudEventSubscription Describe(Subscription resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        CloudEventSubscription? described = null;
        if (resource.Metadata.Annotations?.TryGetValue(AnnotationName, out var annotation) == true)
        {
            try { described = serializer.Deserialize<CloudEventSubscription>(annotation); }
            catch (Exception) { /* an annotation edited by hand is ignored */ }
        }
        described ??= CloudEventSubscriptionFilter.Describe(resource);
        return described with { Id = resource.GetName(), Sink = resource.Spec.Subscriber.Endpoint?.Uri.OriginalString ?? $"sink:{resource.Spec.Subscriber.Sink}", Protocol = CloudEventSubscription.HttpProtocol };
    }

    static ProblemDetailsException Invalid(string detail) => new(new(InvalidSubscriptionProblemType, "Bad Request", (int)HttpStatusCode.BadRequest, detail));

}

/// <summary>
/// Compiles the source, types and filters of CloudEvents subscriptions into Cloud Streams filters, and describes Cloud Streams filters as CloudEvents filters
/// </summary>
public static class CloudEventSubscriptionFilter
{

    abstract record Node;
    sealed record Match(string Dialect, string Attribute, string Value) : Node;
    sealed record All(IReadOnlyList<Node> Nodes) : Node;
    sealed record Any(IReadOnlyList<Node> Nodes) : Node;
    sealed record Not(Node Node) : Node;

    /// <summary>
    /// Compiles the source, types and filters of the specified subscription into a Cloud Streams filter
    /// </summary>
    /// <param name="subscription">The subscription to compile the filter of</param>
    /// <returns>A new <see cref="CloudEventFilter"/>, or null if the subscription selects every cloud event</returns>
    /// <exception cref="ProblemDetailsException">The subscription's filters are invalid, or use an unsupported dialect</exception>
    public static CloudEventFilter? Compile(CloudEventSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        var nodes = new List<Node>();
        if (subscription.Source != null)
        {
            if (string.IsNullOrWhiteSpace(subscription.Source)) throw Invalid("'source' must not be empty");
            nodes.Add(new Match("exact", CloudEventAttributes.Source, subscription.Source));
        }
        if (subscription.Types != null)
        {
            if (subscription.Types.Count == 0 || subscription.Types.Any(string.IsNullOrWhiteSpace)) throw Invalid("'types' must contain at least one type, and no empty one");
            nodes.Add(subscription.Types.Count == 1 ? new Match("exact", CloudEventAttributes.Type, subscription.Types[0]) : new Any([.. subscription.Types.Select(t => new Match("exact", CloudEventAttributes.Type, t))]));
        }
        if (subscription.Filters != null) nodes.AddRange(subscription.Filters.Select((f, i) => Parse(f, $"filters[{i}]")));
        var conditions = Flatten(new All(nodes));
        if (conditions.Count == 0) return null;
        var attributes = ToAttributeFilters(conditions);
        if (attributes != null) return new() { Type = CloudEventFilterType.Attributes, Attributes = attributes };
        return new() { Type = CloudEventFilterType.Expression, Expression = $"${{ {ToExpression(new All(conditions))} }}" };
    }

    /// <summary>
    /// Describes the filter of a Cloud Streams subscription that has not been created through the CloudEvents Subscriptions API, as well as it can
    /// </summary>
    /// <param name="resource">The subscription to describe</param>
    /// <returns>A new <see cref="CloudEventSubscription"/></returns>
    /// <remarks>
    /// A partition by source or type is described as the subscription's source or types, and attribute filters that are anchored literals as <c>exact</c>,
    /// <c>prefix</c> or <c>suffix</c> filters. Other filters cannot be described, and are left out
    /// </remarks>
    public static CloudEventSubscription Describe(Subscription resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var described = new CloudEventSubscription();
        switch (resource.Spec.Partition?.Type)
        {
            case CloudEventPartitionType.BySource:
                described.Source = resource.Spec.Partition.Id;
                break;
            case CloudEventPartitionType.ByType:
                described.Types = [resource.Spec.Partition.Id];
                break;
        }
        if (resource.Spec.Filter?.Type == CloudEventFilterType.Attributes && resource.Spec.Filter.Attributes != null)
        {
            var filters = new Dictionary<string, JsonObject>();
            foreach (var (attribute, pattern) in resource.Spec.Filter.Attributes)
            {
                if (string.IsNullOrEmpty(pattern) || !TryDescribe(pattern, out var dialect, out var value)) continue;
                if (!filters.TryGetValue(dialect, out var filter)) filters[dialect] = filter = [];
                filter[attribute] = value;
            }
            if (filters.Count > 0) described.Filters = [.. filters.Select(f => new JsonObject() { [f.Key] = f.Value })];
        }
        if (resource.Spec.Stream?.Offset is long offset) described.Config = new() { [CloudEventSubscriptionManager.OffsetConfigKey] = offset };
        return described;
    }

    static Node Parse(JsonObject? filter, string path)
    {
        if (filter == null || filter.Count != 1) throw Invalid($"'{path}' must be an object with a single property, named after the filter's dialect");
        var (dialect, value) = filter.First();
        switch (dialect)
        {
            case "exact":
            case "prefix":
            case "suffix":
                if (value is not JsonObject attributes || attributes.Count == 0) throw Invalid($"'{path}.{dialect}' must be an object that maps at least one attribute to a value");
                var matches = new List<Node>();
                foreach (var (attribute, expected) in attributes)
                {
                    if (string.IsNullOrWhiteSpace(attribute)) throw Invalid($"'{path}.{dialect}' must not filter an attribute with an empty name");
                    if (expected is not JsonValue expectedValue || !expectedValue.TryGetValue<string>(out var text) || string.IsNullOrEmpty(text)) throw Invalid($"'{path}.{dialect}.{attribute}' must be a non-empty string");
                    matches.Add(new Match(dialect, attribute, text));
                }
                return matches.Count == 1 ? matches[0] : new All(matches);
            case "all":
            case "any":
                if (value is not JsonArray expressions || expressions.Count == 0) throw Invalid($"'{path}.{dialect}' must be an array of at least one filter expression");
                var nodes = expressions.Select((e, i) => Parse(e as JsonObject, $"{path}.{dialect}[{i}]")).ToList();
                return dialect == "all" ? new All(nodes) : new Any(nodes);
            case "not":
                return new Not(Parse(value as JsonObject, $"{path}.not"));
            case "sql":
                throw Invalid($"'{path}' uses the 'sql' filter dialect, which is not supported");
            default:
                throw Invalid($"'{path}' uses the '{dialect}' filter dialect, which is not supported. Supported dialects: exact, prefix, suffix, all, any, not");
        }
    }

    /// <summary>
    /// Flattens the specified conjunction into the list of the conditions that must all be true
    /// </summary>
    static List<Node> Flatten(Node node) => node is All all ? [.. all.Nodes.SelectMany(Flatten)] : [node];

    /// <summary>
    /// Compiles the specified conditions into attribute filters, if each applies to a different attribute, and is a match, or a choice between exact values
    /// </summary>
    static Dictionary<string, string?>? ToAttributeFilters(IReadOnlyList<Node> conditions)
    {
        var attributes = new Dictionary<string, string?>();
        foreach (var condition in conditions)
        {
            string attribute, pattern;
            switch (condition)
            {
                case Match match:
                    attribute = match.Attribute;
                    pattern = ToPattern(match);
                    break;
                case Any any when any.Nodes.All(n => n is Match { Dialect: "exact" }) && any.Nodes.Cast<Match>().Select(m => m.Attribute).Distinct().Count() == 1:
                    attribute = ((Match)any.Nodes[0]).Attribute;
                    pattern = $"^(?:{string.Join('|', any.Nodes.Cast<Match>().Select(m => Regex.Escape(m.Value)))})\\z";
                    break;
                default:
                    return null;
            }
            if (!attributes.TryAdd(attribute, pattern)) return null;
        }
        return attributes;
    }

    static string ToPattern(Match match) => match.Dialect switch
    {
        "exact" => $"^{Regex.Escape(match.Value)}\\z",
        "prefix" => $"^{Regex.Escape(match.Value)}",
        "suffix" => $"{Regex.Escape(match.Value)}\\z",
        _ => throw new NotSupportedException($"The filter dialect '{match.Dialect}' is not supported")
    };

    /// <summary>
    /// Compiles the specified node into a jq expression, evaluated against the record of a cloud event, whose context attributes are those of its metadata
    /// </summary>
    static string ToExpression(Node node) => node switch
    {
        Match match => ToExpression(match),
        All all => all.Nodes.Count == 0 ? "true" : string.Join(" and ", all.Nodes.Select(n => $"({ToExpression(n)})")),
        Any any => string.Join(" or ", any.Nodes.Select(n => $"({ToExpression(n)})")),
        Not not => $"(({ToExpression(not.Node)}) | not)",
        _ => throw new NotSupportedException()
    };

    static string ToExpression(Match match)
    {
        var attribute = System.Text.Json.JsonSerializer.Serialize(match.Attribute);
        var value = System.Text.Json.JsonSerializer.Serialize(match.Value);
        var test = match.Dialect switch
        {
            "exact" => $"== {value}",
            "prefix" => $"| startswith({value})",
            "suffix" => $"| endswith({value})",
            _ => throw new NotSupportedException($"The filter dialect '{match.Dialect}' is not supported")
        };
        // an attribute the cloud event does not define matches nothing, whatever the dialect
        return $"(.metadata.contextAttributes | has({attribute})) and (.metadata.contextAttributes[{attribute}] | tostring {test})";
    }

    static bool TryDescribe(string pattern, out string dialect, out string value)
    {
        var exact = pattern.StartsWith('^') && pattern.EndsWith("\\z");
        var prefix = pattern.StartsWith('^') && !exact;
        var suffix = pattern.EndsWith("\\z") && !exact;
        dialect = exact ? "exact" : prefix ? "prefix" : suffix ? "suffix" : string.Empty;
        var literal = exact ? pattern[1..^2] : prefix ? pattern[1..] : suffix ? pattern[..^2] : string.Empty;
        value = string.Empty;
        if (dialect.Length == 0 || literal.Length == 0) return false;
        string unescaped;
        try { unescaped = Regex.Unescape(literal); }
        catch (ArgumentException) { return false; }
        // only literals can be described: a pattern that uses regular expression constructs cannot
        if (Regex.Escape(unescaped) != literal) return false;
        value = unescaped;
        return true;
    }

    static ProblemDetailsException Invalid(string detail) => new(new(CloudEventSubscriptionManager.InvalidSubscriptionProblemType, "Bad Request", (int)HttpStatusCode.BadRequest, detail));

}
