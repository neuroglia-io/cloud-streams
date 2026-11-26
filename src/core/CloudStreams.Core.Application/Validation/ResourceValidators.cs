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
using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Data.Expressions;
using System.Text.RegularExpressions;

namespace CloudStreams.Core.Application.Validation;

/// <summary>
/// Defines rules shared by the validators of Cloud Streams resources
/// </summary>
/// <remarks>
/// Resource definitions only describe the shape of resources. These rules catch the values that are well-formed but cannot work, and would otherwise only fail at
/// runtime, faulting subscriptions or crashing handlers: regular expressions that do not compile, incomplete backoffs, non-positive rates, and so on (see #170)
/// </remarks>
public static class ResourceValidationRules
{

    /// <summary>
    /// Determines whether the specified value is a valid regular expression
    /// </summary>
    /// <param name="pattern">The pattern to check</param>
    /// <returns>A boolean indicating whether or not the specified value is a valid regular expression</returns>
    public static bool IsValidRegex(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return true;
        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the specified value is either a runtime expression or a valid regular expression
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>A boolean indicating whether or not the specified value is either a runtime expression or a valid regular expression</returns>
    public static bool IsValidFilterValue(string? value) => string.IsNullOrEmpty(value) || value.IsRuntimeExpression() || IsValidRegex(value);

    /// <summary>
    /// Determines whether the specified URI is an absolute HTTP or HTTPS URI
    /// </summary>
    /// <param name="uri">The URI to check</param>
    /// <returns>A boolean indicating whether or not the specified URI is an absolute HTTP or HTTPS URI</returns>
    public static bool IsHttpUri(Uri? uri) => uri != null && uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Determines whether the specified value is a valid resource name: lower-case letters, digits and dashes, starting and ending with a letter or a digit, up to
    /// 63 characters
    /// </summary>
    /// <param name="name">The name to check</param>
    /// <returns>A boolean indicating whether or not the specified value is a valid resource name</returns>
    public static bool IsResourceName(string? name) => name != null && Regex.IsMatch(name, "^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$");

}

/// <summary>
/// Represents the service used to validate <see cref="HttpClientRetryPolicy"/> instances
/// </summary>
public class HttpClientRetryPolicyValidator
    : AbstractValidator<HttpClientRetryPolicy>
{

    /// <summary>
    /// Initializes a new <see cref="HttpClientRetryPolicyValidator"/>
    /// </summary>
    public HttpClientRetryPolicyValidator()
    {
        this.RuleFor(p => p.MaxAttempts).GreaterThanOrEqualTo(-1).When(p => p.MaxAttempts.HasValue).OverridePropertyName("maxAttempts").WithName("maxAttempts").WithMessage("'maxAttempts' must be positive, or -1 to retry forever");
        this.RuleForEach(p => p.StatusCodes).InclusiveBetween(100, 599).OverridePropertyName("statusCodes").WithName("statusCodes").WithMessage("'statusCodes' must only contain HTTP status codes, between 100 and 599");
        this.RuleFor(p => p.BackoffDuration).NotNull().OverridePropertyName("backoffDuration").WithName("backoffDuration");
        this.RuleFor(p => p.BackoffDuration).SetValidator(new RetryBackoffDurationValidator()!).When(p => p.BackoffDuration != null).OverridePropertyName("backoffDuration").WithName("backoffDuration");
        this.When(p => p.CircuitBreaker != null, () =>
        {
            this.RuleFor(p => p.CircuitBreaker!.BreakAfter).GreaterThan(0).OverridePropertyName("circuitBreaker.breakAfter").WithName("circuitBreaker.breakAfter");
            this.RuleFor(p => p.CircuitBreaker!.BreakDuration).NotNull().OverridePropertyName("circuitBreaker.breakDuration").WithName("circuitBreaker.breakDuration");
        });
    }

}

/// <summary>
/// Represents the service used to validate <see cref="RetryBackoffDuration"/>s
/// </summary>
public class RetryBackoffDurationValidator
    : AbstractValidator<RetryBackoffDuration>
{

    /// <summary>
    /// Initializes a new <see cref="RetryBackoffDurationValidator"/>
    /// </summary>
    public RetryBackoffDurationValidator()
    {
        this.RuleFor(d => d.Period).NotNull().OverridePropertyName("period").WithName("period").WithMessage("'period' is required");
        this.RuleFor(d => d.Exponent).Must(e => e is > 0).When(d => d.Type == RetryBackoffDurationType.Exponential).OverridePropertyName("exponent").WithName("exponent").WithMessage("'exponent' is required, and must be positive, for exponential backoffs");
    }

}

/// <summary>
/// Represents the service used to validate <see cref="ServiceConfiguration"/>s
/// </summary>
public class ServiceConfigurationValidator
    : AbstractValidator<ServiceConfiguration>
{

    /// <summary>
    /// Initializes a new <see cref="ServiceConfigurationValidator"/>
    /// </summary>
    public ServiceConfigurationValidator()
    {
        this.RuleFor(s => s.Uri).Must(ResourceValidationRules.IsHttpUri).WithMessage("'uri' must be an absolute HTTP or HTTPS URI");
        this.When(s => s.HealthChecks != null, () =>
        {
            this.RuleFor(s => s.HealthChecks!.Request).NotNull().OverridePropertyName("healthChecks.request").WithName("healthChecks.request");
            this.RuleFor(s => s.HealthChecks!.Request.Method).NotEmpty().When(s => s.HealthChecks!.Request != null).OverridePropertyName("healthChecks.request.method").WithName("healthChecks.request.method");
            this.RuleFor(s => s.HealthChecks!.Request.Path).NotEmpty().When(s => s.HealthChecks!.Request != null).OverridePropertyName("healthChecks.request.path").WithName("healthChecks.request.path");
        });
    }

}

/// <summary>
/// Represents the service used to validate <see cref="Subscription"/>s
/// </summary>
public class SubscriptionValidator
    : AbstractValidator<Subscription>
{

    /// <summary>
    /// Initializes a new <see cref="SubscriptionValidator"/>
    /// </summary>
    public SubscriptionValidator()
    {
        this.RuleFor(s => s.Spec).NotNull();
        this.When(s => s.Spec != null, () =>
        {
            this.RuleFor(s => s.Spec.Subscriber).NotNull().OverridePropertyName("spec.subscriber").WithName("spec.subscriber");
            this.When(s => s.Spec.Subscriber != null, () =>
            {
                this.RuleFor(s => s.Spec.Subscriber).Must(s => (s.Endpoint == null) != string.IsNullOrWhiteSpace(s.Sink)).OverridePropertyName("spec.subscriber").WithName("spec.subscriber")
                    .WithMessage("Exactly one of 'spec.subscriber.endpoint' and 'spec.subscriber.sink' must be set");
                this.RuleFor(s => s.Spec.Subscriber.Endpoint).Custom((endpoint, context) => { foreach (var (path, error) in EndpointValidator.Validate(endpoint, false, "spec.subscriber.endpoint")) context.AddFailure(path, error); }).When(s => s.Spec.Subscriber.Endpoint != null);
                this.RuleFor(s => s.Spec.Subscriber.Sink).Must(ResourceValidationRules.IsResourceName).When(s => s.Spec.Subscriber.Sink != null).OverridePropertyName("spec.subscriber.sink").WithName("spec.subscriber.sink")
                    .WithMessage("'spec.subscriber.sink' must be the name of a sink");
                this.RuleFor(s => s.Spec.Subscriber.RateLimit).GreaterThan(0).When(s => s.Spec.Subscriber.RateLimit.HasValue).OverridePropertyName("spec.subscriber.rateLimit").WithName("spec.subscriber.rateLimit");
                this.RuleFor(s => s.Spec.Subscriber.RetryPolicy!).SetValidator(new HttpClientRetryPolicyValidator()).When(s => s.Spec.Subscriber.RetryPolicy != null).OverridePropertyName("spec.subscriber.retryPolicy").WithName("spec.subscriber.retryPolicy");
                this.RuleFor(s => s.Spec.Subscriber.Batch!.MaxSize).GreaterThan(0).When(s => s.Spec.Subscriber.Batch != null).OverridePropertyName("spec.subscriber.batch.maxSize").WithName("spec.subscriber.batch.maxSize");
                this.RuleFor(s => s.Spec.Subscriber.Batch!.Linger).Must(l => l!.ToTimeSpan() >= TimeSpan.Zero).When(s => s.Spec.Subscriber.Batch?.Linger != null).OverridePropertyName("spec.subscriber.batch.linger").WithName("spec.subscriber.batch.linger");
                this.RuleFor(s => s.Spec.Subscriber.Ordering!.Key).Matches("^[a-z0-9]+$").When(s => s.Spec.Subscriber.Ordering != null).OverridePropertyName("spec.subscriber.ordering.key").WithName("spec.subscriber.ordering.key")
                    .WithMessage("'spec.subscriber.ordering.key' must be the name of a context attribute, which only contains lower-case letters and digits");
                this.RuleFor(s => s.Spec.Subscriber.Ordering!.MaxConcurrency).GreaterThan(0).When(s => s.Spec.Subscriber.Ordering?.MaxConcurrency != null).OverridePropertyName("spec.subscriber.ordering.maxConcurrency").WithName("spec.subscriber.ordering.maxConcurrency");
                this.RuleFor(s => s.Spec.Subscriber).Must(s => s.Batch == null || s.Ordering == null).OverridePropertyName("spec.subscriber").WithName("spec.subscriber")
                    .WithMessage("'spec.subscriber.batch' and 'spec.subscriber.ordering' cannot be combined");
            });
            this.When(s => s.Spec.Partition != null, () =>
            {
                this.RuleFor(s => s.Spec.Partition!.Id).NotEmpty().OverridePropertyName("spec.partition.id").WithName("spec.partition.id");
                this.RuleFor(s => s.Spec.Partition!.Id).Must(id => Uri.TryCreate(id, UriKind.RelativeOrAbsolute, out _)).When(s => s.Spec.Partition!.Type == CloudEventPartitionType.BySource && !string.IsNullOrEmpty(s.Spec.Partition.Id))
                    .OverridePropertyName("spec.partition.id").WithName("spec.partition.id").WithMessage("The id of a 'by-source' partition must be a URI");
            });
            this.When(s => s.Spec.Filter != null, () =>
            {
                this.RuleFor(s => s.Spec.Filter!.Attributes).NotEmpty().When(s => s.Spec.Filter!.Type == CloudEventFilterType.Attributes).OverridePropertyName("spec.filter.attributes").WithName("spec.filter.attributes");
                this.RuleForEach(s => s.Spec.Filter!.Attributes).Must(a => ResourceValidationRules.IsValidFilterValue(a.Value)).OverridePropertyName("spec.filter.attributes").WithName("spec.filter.attributes")
                    .When(s => s.Spec.Filter!.Type == CloudEventFilterType.Attributes && s.Spec.Filter.Attributes != null)
                    .WithMessage((_, a) => $"The filter of attribute '{a.Key}' is neither a runtime expression nor a valid regular expression");
                this.RuleFor(s => s.Spec.Filter!.Expression).NotEmpty().When(s => s.Spec.Filter!.Type == CloudEventFilterType.Expression).OverridePropertyName("spec.filter.expression").WithName("spec.filter.expression");
            });
            this.When(s => s.Spec.Mutation != null, () =>
            {
                this.RuleFor(s => s.Spec.Mutation!.Expression).NotNull().When(s => s.Spec.Mutation!.Type == CloudEventMutationType.Expression).OverridePropertyName("spec.mutation.expression").WithName("spec.mutation.expression");
                this.RuleFor(s => s.Spec.Mutation!.Webhook).NotNull().When(s => s.Spec.Mutation!.Type == CloudEventMutationType.Webhook).OverridePropertyName("spec.mutation.webhook").WithName("spec.mutation.webhook");
                this.RuleFor(s => s.Spec.Mutation!.Webhook!.ServiceUri).Must(ResourceValidationRules.IsHttpUri).When(s => s.Spec.Mutation!.Type == CloudEventMutationType.Webhook && s.Spec.Mutation.Webhook != null)
                    .OverridePropertyName("spec.mutation.webhook.serviceUri").WithName("spec.mutation.webhook.serviceUri").WithMessage("'spec.mutation.webhook.serviceUri' must be an absolute HTTP or HTTPS URI");
            });
            this.RuleFor(s => s.Spec.Stream!.Offset).GreaterThanOrEqualTo(StreamPosition.EndOfStream).When(s => s.Spec.Stream?.Offset != null).OverridePropertyName("spec.stream.offset").WithName("spec.stream.offset");
            this.RuleFor(s => s.Spec.DeadLetter!.Endpoint).Custom((endpoint, context) => { foreach (var (path, error) in EndpointValidator.Validate(endpoint, false, "spec.deadLetter.endpoint")) context.AddFailure(path, error); }).When(s => s.Spec.DeadLetter?.Endpoint != null);
            this.RuleFor(s => s.Spec.DeadLetter!.Sink).Must(ResourceValidationRules.IsResourceName).When(s => s.Spec.DeadLetter?.Sink != null).OverridePropertyName("spec.deadLetter.sink").WithName("spec.deadLetter.sink")
                .WithMessage("'spec.deadLetter.sink' must be the name of a sink");
            this.RuleFor(s => s.Spec.DeadLetter).Must(d => d!.Endpoint == null || d.Sink == null).When(s => s.Spec.DeadLetter != null).OverridePropertyName("spec.deadLetter").WithName("spec.deadLetter")
                .WithMessage("'spec.deadLetter.endpoint' and 'spec.deadLetter.sink' cannot be combined");
        });
    }

}

/// <summary>
/// Represents the service used to validate <see cref="Broker"/>s
/// </summary>
public class BrokerValidator
    : AbstractValidator<Broker>
{

    /// <summary>
    /// Initializes a new <see cref="BrokerValidator"/>
    /// </summary>
    public BrokerValidator()
    {
        this.RuleFor(b => b.Spec).NotNull();
        this.When(b => b.Spec != null, () =>
        {
            this.RuleFor(b => b.Spec.Dispatch!.RetryPolicy!).SetValidator(new HttpClientRetryPolicyValidator()).When(b => b.Spec.Dispatch?.RetryPolicy != null).OverridePropertyName("spec.dispatch.retryPolicy").WithName("spec.dispatch.retryPolicy");
            this.RuleFor(b => b.Spec.Dispatch!.Sequencing!.AttributeName).NotEmpty().When(b => b.Spec.Dispatch?.Sequencing?.Strategy == CloudEventSequencingStrategy.Attribute)
                .OverridePropertyName("spec.dispatch.sequencing.attributeName").WithName("spec.dispatch.sequencing.attributeName");
            this.RuleFor(b => b.Spec.Dispatch!.Ack!.CallbackUri).Must(ResourceValidationRules.IsHttpUri).When(b => b.Spec.Dispatch?.Ack?.CallbackUri != null)
                .OverridePropertyName("spec.dispatch.ack.callbackUri").WithName("spec.dispatch.ack.callbackUri").WithMessage("'spec.dispatch.ack.callbackUri' must be an absolute HTTP or HTTPS URI");
            this.RuleFor(b => b.Spec.Service!).SetValidator(new ServiceConfigurationValidator()).When(b => b.Spec.Service != null).OverridePropertyName("spec.service").WithName("spec.service");
        });
    }

}

/// <summary>
/// Represents the service used to validate <see cref="Gateway"/>s
/// </summary>
public class GatewayValidator
    : AbstractValidator<Gateway>
{

    /// <summary>
    /// Initializes a new <see cref="GatewayValidator"/>
    /// </summary>
    public GatewayValidator()
    {
        this.RuleFor(g => g.Spec).NotNull();
        this.When(g => g.Spec != null, () =>
        {
            this.RuleForEach(g => g.Spec.Authorization!.Rules).SetValidator(new CloudEventAuthorizationRuleValidator()).When(g => g.Spec.Authorization?.Rules != null).OverridePropertyName("spec.authorization.rules").WithName("spec.authorization.rules");
            this.RuleForEach(g => g.Spec.Sources).ChildRules(source =>
            {
                source.RuleFor(s => s.Uri).NotNull().OverridePropertyName("uri").WithName("uri");
                source.RuleForEach(s => s.Authorization!.Rules).SetValidator(new CloudEventAuthorizationRuleValidator()).When(s => s.Authorization?.Rules != null);
            }).When(g => g.Spec.Sources != null).OverridePropertyName("spec.sources").WithName("spec.sources");
            this.RuleForEach(g => g.Spec.Events).ChildRules(events =>
            {
                events.RuleFor(e => e.Source).Must(p => p?.Trim() == "*" || ResourceValidationRules.IsValidRegex(p)).OverridePropertyName("source").WithName("source").WithMessage("'source' must be '*' or a valid regular expression");
                events.RuleFor(e => e.Type).Must(p => p?.Trim() == "*" || ResourceValidationRules.IsValidRegex(p)).OverridePropertyName("type").WithName("type").WithMessage("'type' must be '*' or a valid regular expression");
                events.RuleFor(e => e.Schema!.Name).NotEmpty().When(e => e.Schema != null).OverridePropertyName("schema.name").WithName("schema.name");
                events.RuleFor(e => e.Schema!.Version).GreaterThan(0).When(e => e.Schema?.Version != null).OverridePropertyName("schema.version").WithName("schema.version");
            }).When(g => g.Spec.Events != null).OverridePropertyName("spec.events").WithName("spec.events");
            this.When(g => g.Spec.SplitHorizon != null, () =>
            {
                this.RuleFor(g => g.Spec.SplitHorizon!.MaxChainLength).GreaterThan(0u).When(g => g.Spec.SplitHorizon!.MaxChainLength.HasValue).OverridePropertyName("spec.splitHorizon.maxChainLength").WithName("spec.splitHorizon.maxChainLength");
                this.RuleFor(g => g.Spec.SplitHorizon!.OriginAttribute).Matches("^[a-z0-9]+$").OverridePropertyName("spec.splitHorizon.originAttribute").WithName("spec.splitHorizon.originAttribute")
                    .WithMessage("'spec.splitHorizon.originAttribute' must only contain lower-case letters and digits, as required of extension attribute names");
            });
            this.RuleFor(g => g.Spec.Service!).SetValidator(new ServiceConfigurationValidator()).When(g => g.Spec.Service != null).OverridePropertyName("spec.service").WithName("spec.service");
            this.When(g => g.Spec.Ingestion != null, () =>
            {
                this.RuleFor(g => g.Spec.Ingestion!.MaxRequestBodySize).GreaterThan(0).When(g => g.Spec.Ingestion!.MaxRequestBodySize.HasValue).OverridePropertyName("spec.ingestion.maxRequestBodySize").WithName("spec.ingestion.maxRequestBodySize");
                this.RuleFor(g => g.Spec.Ingestion!.MaxBatchSize).GreaterThan(0).When(g => g.Spec.Ingestion!.MaxBatchSize.HasValue).OverridePropertyName("spec.ingestion.maxBatchSize").WithName("spec.ingestion.maxBatchSize");
                this.RuleFor(g => g.Spec.Ingestion!.Deduplication!.Window).Must(w => w != null && w.ToTimeSpan() > TimeSpan.Zero).When(g => g.Spec.Ingestion!.Deduplication != null)
                    .OverridePropertyName("spec.ingestion.deduplication.window").WithName("spec.ingestion.deduplication.window").WithMessage("'spec.ingestion.deduplication.window' is required, and must be positive");
                this.RuleForEach(g => g.Spec.Ingestion!.RateLimits).ChildRules(limit =>
                {
                    limit.RuleFor(l => l.Partition).Must(p => CloudEventRateLimitPartitioning.AsEnumerable().Contains(p)).OverridePropertyName("partition").WithName("partition").WithMessage($"'partition' must be one of: {string.Join(", ", CloudEventRateLimitPartitioning.AsEnumerable())}");
                    limit.RuleFor(l => l.Limit).GreaterThan(0).OverridePropertyName("limit").WithName("limit");
                    limit.RuleFor(l => l.Period).Must(p => p != null && p.ToTimeSpan() > TimeSpan.Zero).OverridePropertyName("period").WithName("period").WithMessage("'period' is required, and must be positive");
                }).When(g => g.Spec.Ingestion!.RateLimits != null).OverridePropertyName("spec.ingestion.rateLimits").WithName("spec.ingestion.rateLimits");
                this.RuleFor(g => g.Spec.Ingestion!.Feeds).Custom((feeds, context) =>
                {
                    for (var i = 0; i < feeds!.Count; i++)
                    {
                        var feed = feeds[i];
                        var path = $"spec.ingestion.feeds[{i}]";
                        if (feed == null) context.AddFailure(path, $"'{path}' must be the name of a feed, or a feed with a name and an endpoint");
                        else if (feed.Feed != null)
                        {
                            if (!ResourceValidationRules.IsResourceName(feed.Feed)) context.AddFailure(path, $"'{feed.Feed}' is not the name of a feed");
                        }
                        else
                        {
                            if (!ResourceValidationRules.IsResourceName(feed.Name)) context.AddFailure($"{path}.name", $"'{path}.name' must be made of lower-case letters, digits and dashes, starting and ending with a letter or a digit, up to 63 characters");
                            foreach (var (errorPath, error) in EndpointValidator.Validate(feed.Endpoint, true, $"{path}.endpoint")) context.AddFailure(errorPath, error);
                        }
                    }
                    var duplicate = feeds.Where(f => f?.EffectiveName != null).GroupBy(f => f.EffectiveName).FirstOrDefault(g => g.Count() > 1);
                    if (duplicate != null) context.AddFailure("spec.ingestion.feeds", $"'spec.ingestion.feeds' lists the feed '{duplicate.Key}' more than once: the names of feeds identify them as publishers, and must be unique");
                }).When(g => g.Spec.Ingestion!.Feeds != null);
                this.RuleFor(g => g.Spec.Ingestion!.Publishers).Custom((publishers, context) =>
                {
                    for (var i = 0; i < publishers!.Count; i++)
                    {
                        var publisher = publishers[i];
                        var path = $"spec.ingestion.publishers[{i}]";
                        if (publisher == null)
                        {
                            context.AddFailure(path, $"'{path}' must be a publisher");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(publisher.Name)) context.AddFailure($"{path}.name", $"'{path}.name' is required");
                        // a publisher identified by nothing would match no request, and one identified by both could be mistaken for either
                        if ((publisher.ApiKey != null) == publisher.UsesToken) context.AddFailure(path, $"'{path}' must be identified either by an 'apiKey', or by the 'subject' or 'claims' of its bearer tokens");
                        if (publisher.ApiKey != null && string.IsNullOrWhiteSpace(publisher.ApiKey.Key) && publisher.ApiKey.Use == null) context.AddFailure($"{path}.apiKey", $"'{path}.apiKey' must set its 'key', or the secret that holds it with 'use'");
                        if (publisher.ApiKey?.Use != null && publisher.ApiKey.Use.Provider != null && !SecretProviders.AsEnumerable().Contains(publisher.ApiKey.Use.Provider)) context.AddFailure($"{path}.apiKey.use", $"'{path}.apiKey.use' must name one of the providers: {string.Join(", ", SecretProviders.AsEnumerable())}");
                        if (publisher.Sources?.Any(string.IsNullOrWhiteSpace) == true) context.AddFailure($"{path}.sources", $"'{path}.sources' must not contain empty patterns");
                    }
                    var duplicate = publishers.Where(p => !string.IsNullOrWhiteSpace(p?.Name)).GroupBy(p => p.Name).FirstOrDefault(g => g.Count() > 1);
                    if (duplicate != null) context.AddFailure("spec.ingestion.publishers", $"'spec.ingestion.publishers' lists the publisher '{duplicate.Key}' more than once");
                }).When(g => g.Spec.Ingestion!.Publishers != null);
            });
        });
    }

}

/// <summary>
/// Represents the service used to validate <see cref="CloudEventAuthorizationRule"/>s
/// </summary>
public class CloudEventAuthorizationRuleValidator
    : AbstractValidator<CloudEventAuthorizationRule>
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventAuthorizationRuleValidator"/>
    /// </summary>
    public CloudEventAuthorizationRuleValidator()
    {
        this.RuleFor(r => r.AttributeName).NotEmpty().When(r => r.Type == CloudEventAuthorizationRuleType.Attribute).OverridePropertyName("attributeName").WithName("attributeName").WithMessage("'attributeName' is required by attribute rules");
        this.RuleFor(r => r.AttributeValue).Must(ResourceValidationRules.IsValidRegex).When(r => r.Type == CloudEventAuthorizationRuleType.Attribute).OverridePropertyName("attributeValue").WithName("attributeValue").WithMessage("'attributeValue' must be a valid regular expression");
        this.RuleFor(r => r.MaxSize).Must(m => m is > 0).When(r => r.Type == CloudEventAuthorizationRuleType.Payload).OverridePropertyName("maxSize").WithName("maxSize").WithMessage("'maxSize' is required, and must be positive, by payload rules");
        this.RuleFor(r => r).Must(r => r.From == null || r.To == null || r.From <= r.To).When(r => r.Type == CloudEventAuthorizationRuleType.Temporary).OverridePropertyName("to").WithName("to").WithMessage("'to' must not precede 'from'");
        this.RuleFor(r => r).Must(r => r.From != null || r.To != null).When(r => r.Type is CloudEventAuthorizationRuleType.Temporary or CloudEventAuthorizationRuleType.TimeOfDay).OverridePropertyName("from").WithName("from").WithMessage("'from', 'to' or both are required by time-based rules");
    }

}

/// <summary>
/// Represents the service used to validate <see cref="Sink"/>s (see #218)
/// </summary>
public class SinkValidator
    : AbstractValidator<Sink>
{

    /// <summary>
    /// Initializes a new <see cref="SinkValidator"/>
    /// </summary>
    public SinkValidator()
    {
        this.RuleFor(s => s.Spec).NotNull();
        this.RuleFor(s => s.Spec.Endpoint).Custom((endpoint, context) => { foreach (var (path, error) in EndpointValidator.Validate(endpoint, false, "spec.endpoint")) context.AddFailure(path, error); }).When(s => s.Spec != null);
    }

}

/// <summary>
/// Represents the service used to validate <see cref="Feed"/>s (see #218)
/// </summary>
public class FeedValidator
    : AbstractValidator<Feed>
{

    /// <summary>
    /// Initializes a new <see cref="FeedValidator"/>
    /// </summary>
    public FeedValidator()
    {
        this.RuleFor(s => s.Spec).NotNull();
        this.RuleFor(s => s.Spec.Endpoint).Custom((endpoint, context) => { foreach (var (path, error) in EndpointValidator.Validate(endpoint, true, "spec.endpoint")) context.AddFailure(path, error); }).When(s => s.Spec != null);
    }

}
