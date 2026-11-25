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

using FluentValidation;

namespace CloudStreams.Broker.Application.Services.Delivery;

/// <summary>
/// Represents the pipeline the records consumed by a subscription go through before they are delivered: they are filtered, then mutated, and the mutated cloud
/// events are validated (see #163)
/// </summary>
/// <param name="expressionEvaluator">The service used to evaluate the runtime expressions of filters and mutations</param>
/// <param name="validators">The services used to validate mutated cloud events</param>
/// <param name="httpClient">The service used to call mutation webhooks</param>
/// <param name="serializer">The service used to deserialize the cloud events mutation webhooks respond with</param>
public class SubscriptionEventPipeline(IExpressionEvaluator expressionEvaluator, IEnumerable<IValidator<CloudEvent>> validators, HttpClient httpClient, IJsonSerializer serializer)
{

    /// <summary>
    /// Gets the media types a mutation webhook may respond with
    /// </summary>
    public static readonly string[] MutationWebhookMediaTypes = [MediaTypeNames.Application.Json, CloudEventContentType.Json];

    /// <summary>
    /// Gets the service used to evaluate the runtime expressions of filters and mutations
    /// </summary>
    protected IExpressionEvaluator ExpressionEvaluator { get; } = expressionEvaluator;

    /// <summary>
    /// Gets the services used to validate mutated cloud events, materialized once, so that enumerating them never runs a validator twice
    /// </summary>
    protected IReadOnlyList<IValidator<CloudEvent>> Validators { get; } = [.. validators];

    /// <summary>
    /// Gets the service used to call mutation webhooks
    /// </summary>
    protected HttpClient HttpClient { get; } = httpClient;

    /// <summary>
    /// Gets the service used to deserialize the cloud events mutation webhooks respond with
    /// </summary>
    protected IJsonSerializer Serializer { get; } = serializer;

    /// <summary>
    /// Determines whether or not the specified subscription selects the specified record
    /// </summary>
    /// <param name="subscription">The subscription whose filter to apply</param>
    /// <param name="e">The record to filter</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether or not the subscription selects the record</returns>
    public virtual ValueTask<bool> FiltersAsync(Subscription subscription, CloudEventRecord e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(e);
        var filter = subscription.Spec.Filter;
        if (filter == null) return ValueTask.FromResult(true);
        return filter.Type switch
        {
            CloudEventFilterType.Attributes => this.FiltersAsync(e, filter.Attributes!, cancellationToken),
            CloudEventFilterType.Expression => this.FiltersAsync(e, filter.Expression!, cancellationToken),
            _ => throw new NotSupportedException($"The specified {nameof(CloudEventFilterType)} '{EnumHelper.Stringify(filter.Type)}' is not supported")
        };
    }

    /// <summary>
    /// Determines whether or not the specified record matches the specified attribute filters
    /// </summary>
    /// <param name="e">The record to filter</param>
    /// <param name="attributeFilters">The key/value mappings of the attributes to filter the record by</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether or not the record matches the attribute filters</returns>
    public virtual async ValueTask<bool> FiltersAsync(CloudEventRecord e, IDictionary<string, string?> attributeFilters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(attributeFilters);
        var attributes = e.Metadata.ContextAttributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        foreach (var attributeFilter in attributeFilters)
        {
            if (!attributes.TryGetValue(attributeFilter.Key, out var attributeValue) || string.IsNullOrWhiteSpace(attributeValue)) return false;
            if (string.IsNullOrWhiteSpace(attributeFilter.Value)) continue;
            // the filter value, not the event's attribute value, determines whether the filter is a runtime expression or a regular expression
            if (attributeFilter.Value.IsRuntimeExpression())
            {
                if (!await this.ExpressionEvaluator.EvaluateConditionAsync(attributeFilter.Value, attributeValue, cancellationToken: cancellationToken).ConfigureAwait(false)) return false;
            }
            else if (!CloudStreamsRegex.IsMatch(attributeValue, attributeFilter.Value)) return false;
        }
        return true;
    }

    /// <summary>
    /// Determines whether or not the specified record matches the specified runtime expression
    /// </summary>
    /// <param name="e">The record to filter</param>
    /// <param name="expression">The runtime expression to evaluate</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether or not the record matches the runtime expression</returns>
    public virtual async ValueTask<bool> FiltersAsync(CloudEventRecord e, string expression, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        return await this.ExpressionEvaluator.EvaluateConditionAsync(expression, e, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Mutates the specified cloud event as configured by the specified subscription, and validates the result
    /// </summary>
    /// <param name="subscription">The subscription whose mutation to apply</param>
    /// <param name="e">The cloud event to mutate</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>, which bounds the duration of a webhook based mutation</param>
    /// <returns>The mutated cloud event, or a copy of the cloud event if the subscription does not mutate cloud events</returns>
    /// <exception cref="FormatException">The mutated cloud event is invalid</exception>
    public virtual async Task<CloudEvent> MutateAsync(Subscription subscription, CloudEvent e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(e);
        var mutation = subscription.Spec.Mutation;
        if (mutation == null) return e.Clone()!;
        var mutated = mutation.Type switch
        {
            CloudEventMutationType.Expression => await this.MutateAsync(e, mutation.Expression!).ConfigureAwait(false),
            CloudEventMutationType.Webhook => await this.MutateAsync(e, mutation.Webhook!, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The specified {nameof(CloudEventMutationType)} '{EnumHelper.Stringify(mutation.Type)}' is not supported"),
        } ?? throw new NullReferenceException("Failed to mutate the specified cloud event");
        await this.ValidateAsync(mutated, cancellationToken).ConfigureAwait(false);
        return mutated;
    }

    /// <summary>
    /// Performs a runtime expression based mutation on the specified cloud event
    /// </summary>
    /// <param name="e">The cloud event to mutate</param>
    /// <param name="mutation">The runtime expression, or the object whose values may be runtime expressions, that evaluates to the mutated cloud event</param>
    /// <returns>The mutated cloud event</returns>
    public virtual async Task<CloudEvent?> MutateAsync(CloudEvent e, object mutation)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(mutation);
        if (mutation is string expression) return await this.ExpressionEvaluator.EvaluateAsync<CloudEvent>(expression, e).ConfigureAwait(false);
        else return await this.ExpressionEvaluator.EvaluateAsync<CloudEvent>(mutation, e).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs a webhook based mutation on the specified cloud event
    /// </summary>
    /// <param name="e">The cloud event to mutate</param>
    /// <param name="webhook">An object used to configure the webhook to invoke</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>, which bounds the duration of the call to the webhook</param>
    /// <returns>The mutated cloud event</returns>
    /// <exception cref="HttpRequestException">The webhook could not be reached, or responded with a non-success status code</exception>
    public virtual async Task<CloudEvent?> MutateAsync(CloudEvent e, Webhook webhook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(webhook);
        using var requestContent = e.ToHttpContent();
        using var request = new HttpRequestMessage(HttpMethod.Post, webhook.ServiceUri) { Content = requestContent };
        foreach (var mediaType in MutationWebhookMediaTypes) request.Headers.Accept.Add(new(mediaType));
        using var response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"The mutation webhook '{webhook.ServiceUri}' responded with a non-success status code '{response.StatusCode}'", null, response.StatusCode);
        var mediaTypeName = response.Content.Headers.ContentType?.MediaType;
        if (!MutationWebhookMediaTypes.Contains(mediaTypeName, StringComparer.OrdinalIgnoreCase)) throw new FormatException($"The mutation webhook '{webhook.ServiceUri}' responded with an unexpected content type '{mediaTypeName}'. Expected one of: {string.Join(", ", MutationWebhookMediaTypes)}");
        return this.Serializer.Deserialize<CloudEvent>(responseContent);
    }

    /// <summary>
    /// Validates the specified mutated cloud event
    /// </summary>
    /// <param name="e">The cloud event to validate</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <exception cref="FormatException">The cloud event is invalid</exception>
    public virtual async Task ValidateAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        var validationResults = await Task.WhenAll(this.Validators.Select(v => v.ValidateAsync(e, cancellationToken))).ConfigureAwait(false);
        var errors = validationResults.Where(r => !r.IsValid).SelectMany(r => r.Errors).Select(error => $"{error.PropertyName}: {error.ErrorMessage}").ToList();
        if (errors.Count > 0) throw new FormatException($"The mutated cloud event is invalid: {string.Join("; ", errors)}");
    }

}
