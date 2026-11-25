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

using CloudStreams.Core;
using CloudStreams.Core.Application.Services;
using CloudStreams.Core.Resources;
using CloudStreams.Gateway.Application.Configuration;
using FluentValidation;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neuroglia.Data.Expressions.Services;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Serialization;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Represents the default implementation of the <see cref="ICloudEventAdmissionControl"/> interface
/// </summary>
/// <remarks>
/// Initializes a new <see cref="CloudEventAdmissionControl"/>
/// </remarks>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
/// <param name="logger">The service used to perform logging</param>
/// <param name="gatewayOptions">The service used to access the current <see cref="Configuration.GatewayOptions"/></param>
/// <param name="serializer">The service used to serialize/deserialize objects to/from JSON</param>
/// <param name="metrics">The service used to manage Cloud Streams gateway related metrics</param>
/// <param name="authorizationManager">The service used to manage authorization</param>
/// <param name="schemaGenerator">The service used to generate <see cref="JsonSchema"/>s</param>
/// <param name="schemaRegistry">The service used to register and manage <see cref="JsonSchema"/>s</param>
/// <param name="expressionEvaluator">The service used to evaluate runtime expressions</param>
/// <param name="validators">An <see cref="IEnumerable{T}"/> containing the <see cref="IValidator"/>s used to validate <see cref="CloudEvent"/>s</param>
public class CloudEventAdmissionControl(IServiceProvider serviceProvider, ILogger<CloudEventAdmissionControl> logger, IOptions<GatewayOptions> gatewayOptions, IJsonSerializer serializer, IGatewayMetrics metrics, ICloudEventAuthorizationManager authorizationManager,
    IJsonSchemaGenerator schemaGenerator, IJsonSchemaRegistry schemaRegistry, IExpressionEvaluator expressionEvaluator, IEnumerable<IValidator<CloudEvent>> validators)
        : BackgroundService, ICloudEventAdmissionControl, IDisposable
{

    /// <summary>
    /// Gets the type of the problem that describes a cloud event refused because its publisher is not allowed to publish cloud events of its source
    /// </summary>
    public static readonly Uri SourceNotAllowedProblemType = new("https://cloud-streams.io/problems/source-not-allowed");

    readonly IGatewayMetrics _metrics = metrics;
    bool _disposed;

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// gets the object used to configure a 
    /// </summary>
    protected GatewayOptions GatewayOptions { get; } = gatewayOptions.Value;

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from JSON
    /// </summary>
    protected IJsonSerializer Serializer { get; } = serializer;

    /// <summary>
    /// Gets the service used to manage authorization
    /// </summary>
    protected ICloudEventAuthorizationManager AuthorizationManager { get; } = authorizationManager;

    /// <summary>
    /// Gets the service used to generate <see cref="JsonSchema"/>s
    /// </summary>
    protected IJsonSchemaGenerator SchemaGenerator { get; } = schemaGenerator;

    /// <summary>
    /// Gets the service used to register and manage <see cref="JsonSchema"/>s
    /// </summary>
    protected IJsonSchemaRegistry SchemaRegistry { get; } = schemaRegistry;

    /// <summary>
    /// Gets the service used to manage <see cref="IResource"/>s
    /// </summary>
    protected IResourceRepository Resources => this.ServiceProvider.GetRequiredService<IResourceRepository>();

    /// <summary>
    /// Gets the service used to evaluate runtime expressions
    /// </summary>
    protected IExpressionEvaluator ExpressionEvaluator { get; } = expressionEvaluator;

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> containing the <see cref="IValidator"/>s used to validate <see cref="CloudEvent"/>s
    /// </summary>
    protected IEnumerable<IValidator<CloudEvent>> Validators { get; } = validators;

    /// <summary>
    /// Gets the service used to manage the state of the current <see cref="Core.Resources.Gateway"/>
    /// </summary>
    protected IResourceMonitor<Core.Resources.Gateway>? Configuration { get; set; }

    /// <summary>
    /// Gets a boolean indicating whether or not the gateway's configuration has been loaded, and cloud events can therefore be admitted
    /// </summary>
    public bool IsReady => this.Configuration != null;

    /// <inheritdoc/>
    public Core.Resources.Gateway? Gateway => this.Configuration?.Resource;

    /// <summary>
    /// Converts the specified failed <see cref="OperationResult"/> into a failed <see cref="OperationResult{T}"/>, keeping its errors
    /// </summary>
    /// <param name="result">The failed <see cref="OperationResult"/> to convert</param>
    /// <returns>A new failed <see cref="OperationResult{T}"/></returns>
    /// <remarks><see cref="OperationResult.OfType{T}"/> drops the errors, and with them the reason why the event has been refused</remarks>
    protected static OperationResult<CloudEventDescriptor> Failure(OperationResult result) => new(result.Status, errors: result.Errors?.ToArray());

    /// <inheritdoc/>
    /// <remarks>
    /// The gateway's resource is only created when it does not exist, a conflict with a replica that created it concurrently is tolerated, and transient failures are
    /// retried with backoff rather than stopping the host (see #142)
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("CloudEventAdmissionControl.Execute");
        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                Core.Resources.Gateway? gateway = null;
                try
                {
                    gateway = await this.Resources.GetAsync<Core.Resources.Gateway>(this.GatewayOptions.Name, this.GatewayOptions.Namespace, stoppingToken).ConfigureAwait(false);
                }
                catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { }
                if (gateway == null)
                {
                    try
                    {
                        await this.Resources.AddAsync(new Core.Resources.Gateway(new ResourceMetadata(this.GatewayOptions.Name, this.GatewayOptions.Namespace), new GatewaySpec()), false, stoppingToken).ConfigureAwait(false);
                    }
                    catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.Conflict) { /* created by another replica in the meantime */ }
                }
                this.Configuration = await this.Resources.MonitorAsync<Core.Resources.Gateway>(this.GatewayOptions.Name, this.GatewayOptions.Namespace, false, stoppingToken).ConfigureAwait(false);
                this.Logger.LogInformation("The configuration of gateway '{gateway}' has been loaded: admitting cloud events", this.GatewayOptions.Name);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt - 1), 30));
                this.Logger.LogError(ex, "Failed to load the configuration of gateway '{gateway}' (attempt {attempt}). Cloud events are refused until it is loaded. Retrying in {delay}...", this.GatewayOptions.Name, attempt, delay);
                try { await Task.Delay(delay, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <inheritdoc/>
    public virtual async Task<OperationResult<CloudEventDescriptor>> EvaluateAsync(CloudEvent e, IReadOnlyCollection<string>? allowedSources = null, CancellationToken cancellationToken = default)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("CloudEventAdmissionControl.Evaluate");
        // until the gateway's configuration has been loaded, its policies are unknown: admitting events would bypass authorization and validation (see #142)
        if (this.Configuration == null)
        {
            return new((int)HttpStatusCode.ServiceUnavailable, errors: [new Error(new Uri("https://cloud-streams.io/problems/gateway-unavailable"), "Service Unavailable", (int)HttpStatusCode.ServiceUnavailable, "The gateway is starting: its configuration has not been loaded yet. Please retry later")]);
        }
        activity?.SetTag("event.id", e.Id);
        activity?.SetTag("event.source", e.Source.ToString());
        activity?.SetTag("event.type", e.Type);
        if(!string.IsNullOrWhiteSpace(e.Subject)) activity?.SetTag("event.subject", e.Subject);
        var validationResults = new List<FluentValidation.Results.ValidationResult>(this.Validators.Count());
        foreach (var validator in this.Validators) validationResults.Add(await validator.ValidateAsync(e, cancellationToken).ConfigureAwait(false));
        if (!validationResults.All(r => r.IsValid))
        {
            this._metrics.IncrementTotalInvalidEvents(InvalidityReason.Attributes);
            return new((int)HttpStatusCode.BadRequest, errors: [new Error(ProblemTypes.SchemaValidationFailed, "Invalid", (int)HttpStatusCode.BadRequest, errors: validationResults
                .Where(r => !r.IsValid)
                .SelectMany(r => r.Errors)
                .GroupBy(e => e.PropertyName)
                .Select(g => new KeyValuePair<string, string[]>(g.Key, g.Select(e => e.ErrorMessage).ToArray())))]);
        }
        this.Logger.LogDebug("Started admission evaluation for cloud event with id '{eventId}'...", e.Id);
        // a publisher bound to sources may only publish cloud events of these sources, whatever the gateway's own policies allow (see #147)
        if (allowedSources != null && !CloudEventSourcePattern.IsMatchAny(e.Source, allowedSources))
        {
            this.Logger.LogDebug("The cloud event with id '{eventId}' has been rejected: its publisher is not allowed to publish cloud events of source '{source}'", e.Id, e.Source);
            this._metrics.IncrementTotalRejectedEvents(RejectionReason.Authorization);
            return new((int)HttpStatusCode.Forbidden, errors: [new Error(SourceNotAllowedProblemType, "Forbidden", (int)HttpStatusCode.Forbidden, $"The publisher is not allowed to publish cloud events of source '{e.Source.OriginalString}'")]);
        }
        var result = await this.AuthorizeAsync(e, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess())
        {
            this.Logger.LogDebug("Admission evaluation failed with status code '{statusCode}' for cloud event with id '{eventId}'", result.Status, e.Id);
            // an ignored loop is a success without data: the publisher is told it has been ignored, and it is counted as such rather than as rejected (see #179)
            if (result.Status == (int)HttpStatusCode.Conflict && this.Configuration?.Resource.Spec.SplitHorizon?.LoopAction == LoopAction.Ignore)
            {
                this._metrics.IncrementTotalIgnoredEvents(e.Source);
                return new OperationResult<CloudEventDescriptor>((int)HttpStatusCode.Accepted);
            }
            // the split-horizon policy is the only one to answer '409 Conflict', the authorization policy answering '401 Unauthorized' or '403 Forbidden'
            this._metrics.IncrementTotalRejectedEvents(result.Status == (int)HttpStatusCode.Conflict ? RejectionReason.SplitHorizon : RejectionReason.Authorization);
            return Failure(result);
        }
        result = await this.ValidateAsync(e, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess())
        {
            this.Logger.LogDebug("Admission evaluation failed with status code '{statusCode}' for cloud event with id '{eventId}'", result.Status, e.Id);
            this._metrics.IncrementTotalInvalidEvents(InvalidityReason.Schema);
            return Failure(result);
        }
        this.Logger.LogDebug("Admission evaluation for cloud event with id '{eventId}' completed successfully", e.Id);
        this.Logger.LogDebug("Extracting metadata from the cloud event with id '{eventId}'", e.Id);
        var metadata = await this.ExtractMetadataAsync(e, cancellationToken).ConfigureAwait(false);
        this.Logger.LogDebug("Metadata successfully extracted from the cloud event with id '{eventId}'", e.Id);
        return new((int)HttpStatusCode.OK, new CloudEventDescriptor(metadata, e.Data));
    }

    /// <summary>
    /// Authorizes the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to authorize</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A <see cref="OperationResult"/> that describes the result of the operation</returns>
    protected virtual async Task<OperationResult> AuthorizeAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        this.Logger.LogDebug("Authorizing cloud event with id '{eventId}'...", e.Id);
        var splitHorizon = this.Configuration?.Resource.Spec.SplitHorizon;
        if (splitHorizon != null && e.ExtensionAttributes?.TryGetValue(splitHorizon.OriginAttribute, out var origin) == true)
        {
            var origins = origin.ToString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var lastOrigin = origins.LastOrDefault();
            var reject = false;
            if (!string.IsNullOrWhiteSpace(lastOrigin) && ((splitHorizon.AllowedOrigins != null && !splitHorizon.AllowedOrigins.Contains(lastOrigin)) || (splitHorizon.ExcludedOrigins != null && splitHorizon.ExcludedOrigins.Contains(lastOrigin)))) reject = true;
            else
            {
                var gatewayName = this.Configuration!.Resource.GetQualifiedName();
                var loopDetected = splitHorizon.EvaluationMode switch
                {
                    HorizonEvaluationMode.Last => origins.LastOrDefault() == gatewayName,
                    HorizonEvaluationMode.Contains => origins.Contains(gatewayName),
                    HorizonEvaluationMode.Single => origins.Length == 1 && origins[0] == gatewayName,
                    _ => throw new NotSupportedException($"The specified {nameof(HorizonEvaluationMode)} '{EnumHelper.Stringify(splitHorizon.EvaluationMode)}' is not supported")
                };
                // an event that went through more gateways than allowed is treated as potentially looping, whether or not this gateway is part of its chain (see #173)
                var chainTooLong = splitHorizon.TrackingMode == HorizonTrackingMode.Chain && splitHorizon.MaxChainLength.HasValue && origins.Length > splitHorizon.MaxChainLength.Value;
                reject = loopDetected || chainTooLong;
            }
            if (reject)
            {
                this.Logger.LogDebug("The cloud event with id '{eventId}' has been rejected because the configured split-horizon policy detected a loop", e.Id);
                return new((int)HttpStatusCode.Conflict);
            }
        }
        var policy = this.Configuration?.Resource.Spec.Sources?.FirstOrDefault(s => s.Uri == e.Source)?.Authorization ?? this.Configuration?.Resource.Spec.Authorization;
        if (policy == null) return new((int)HttpStatusCode.OK);
        var result = await this.AuthorizationManager.EvaluateAsync(e, policy, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess())
        {
            this.Logger.LogDebug("Authorization denied for cloud event with id '{eventId}'", e.Id);
            return result;
        }
        this.Logger.LogDebug("Authorization granted for cloud event with id '{eventId}' completed successfully", e.Id);
        return new((int)HttpStatusCode.OK);
    }

    /// <summary>
    /// Validates the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to validate</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A <see cref="OperationResult"/> that describes the result of the operation</returns>
    protected virtual async Task<OperationResult> ValidateAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        this.Logger.LogDebug("Validating cloud event with id '{eventId}'...", e.Id);
        var policy = this.Configuration?.Resource.Spec.Sources?.FirstOrDefault(s => s.Uri == e.Source)?.Validation ?? this.Configuration?.Resource.Spec.Validation;
        // a registered schema bound to the cloud event's source and type applies whatever the validation policy, unless it skips validation (see #213)
        var binding = this.Configuration?.Resource.Spec.Events?.LastOrDefault(c => c.AppliesTo(e))?.Schema;
        if (binding != null && policy?.SkipValidation != true)
        {
            var resolver = this.ServiceProvider.GetService<RegisteredSchemaResolver>() ?? ActivatorUtilities.CreateInstance<RegisteredSchemaResolver>(this.ServiceProvider);
            var (bound, version, error) = await resolver.ResolveAsync(binding, cancellationToken).ConfigureAwait(false);
            if (bound == null)
            {
                this.Logger.LogWarning("Validation of cloud event with id '{eventId}' failed: {reason}", e.Id, error);
                return new((int)HttpStatusCode.BadRequest, errors: [new Error(ProblemTypes.SchemaValidationFailed, "Invalid data schema", (int)HttpStatusCode.BadRequest, error)]);
            }
            var boundResults = bound.Evaluate(this.Serializer.SerializeToNode(e.Data), new EvaluationOptions() { OutputFormat = OutputFormat.List });
            if (!boundResults.IsValid)
            {
                var errors = (boundResults.Details ?? []).Where(d => d.Errors != null).SelectMany(d => d.Errors!.Select(kvp => new KeyValuePair<string, string>(d.InstanceLocation.ToString() is { Length: > 0 } location ? location : "/", kvp.Value)))
                    .GroupBy(kvp => kvp.Key).Select(g => new KeyValuePair<string, string[]>(g.Key, g.Select(kvp => kvp.Value).ToArray())).ToArray();
                this.Logger.LogDebug("Validation of cloud event with id '{eventId}' against version {version} of schema '{schema}' failed", e.Id, version, binding.Name);
                return new((int)HttpStatusCode.BadRequest, null, [new Error(ProblemTypes.SchemaValidationFailed, "Invalid", (int)HttpStatusCode.BadRequest, $"The data of the cloud event is not valid against version {version} of the schema '{binding.Name}'", errors: errors)]);
            }
            return new((int)HttpStatusCode.OK);
        }
        if (policy == null) return new((int)HttpStatusCode.OK);
        if (policy.SkipValidation)
        {
            this.Logger.LogDebug("Skipping validation of cloud event with id '{eventId}': the validation policy for source '{sourceUri}' is configured to skip validation", e.Id, e.Source);
            return new((int)HttpStatusCode.OK);
        }
        var dataSchemaPolicy = policy.DataSchema;
        JsonSchema? schema = null;
        if (e.DataSchema == null)
        {
            dataSchemaPolicy ??= this.Configuration?.Resource.Spec.Validation?.DataSchema;
            if (dataSchemaPolicy?.Required == true)
            {
                this.Logger.LogDebug("Validation of cloud event with id '{eventId}' failed: the validation policy for source '{sourceUri}' requires the cloud event's 'dataSchema' attribute to be set", e.Id, e.Source);
                return new((int)HttpStatusCode.BadRequest);
            }
            if (dataSchemaPolicy?.AutoGenerate == true && e.Data != null)
            {
                schema = (await this.SchemaGenerator.GenerateAsync(e.Data, new() { Id = e.Type }, cancellationToken).ConfigureAwait(false))!;
                e.DataSchema = schema.BaseUri;
            }
        }
        else
        {
            try
            {
                schema = await this.SchemaRegistry.GetAsync(e.DataSchema, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidDataException)
            {
                // the schema is chosen by the publisher: failing to get it is a problem with the event, not with the gateway (see #141)
                this.Logger.LogDebug("Validation of cloud event with id '{eventId}' failed: {reason}", e.Id, ex.Message);
                return new((int)HttpStatusCode.BadRequest, errors: [new Error(ProblemTypes.SchemaValidationFailed, "Invalid data schema", (int)HttpStatusCode.BadRequest, ex.Message, errors: [new KeyValuePair<string, string[]>(CloudEventAttributes.DataSchema, [ex.Message])])]);
            }
        }
        if (schema != null)
        {
            var validationOptions = new EvaluationOptions() { OutputFormat = OutputFormat.Hierarchical };
            var validationResults = schema.Evaluate(this.Serializer.SerializeToNode(e.Data), validationOptions);
            if (!validationResults.IsValid)
            {
                this.Logger.LogDebug("Validation of cloud event with id '{eventId}' failed: {detail}", e.Id, validationResults);
                return new((int)HttpStatusCode.BadRequest, null, [new Error(ProblemTypes.SchemaValidationFailed, "Invalid", (int)HttpStatusCode.BadRequest, errors: validationResults.Errors?.GroupBy(kvp => kvp.Key).Select(g => new KeyValuePair<string, string[]>(g.Key, g.Select(kvp => kvp.Value).ToArray())))]);
            }
        }
        this.Logger.LogDebug("Cloud event with id '{eventId}' successfully validated", e.Id);
        return new((int)HttpStatusCode.OK);
    }

    /// <summary>
    /// Extracts metadata from the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to extract metadata from</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The metadata extracted from the specified <see cref="CloudEvent"/></returns>
    protected virtual async Task<CloudEventMetadata> ExtractMetadataAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!e.Time.HasValue) e.Time = DateTimeOffset.UtcNow;
        var metadata = new CloudEventMetadata(e.GetContextAttributes().ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        // binary data is not a context attribute, and would otherwise be lost: it is kept with them, the event's data being stored as JSON
        if (e.Data == null && !string.IsNullOrEmpty(e.DataBase64)) metadata.ContextAttributes[CloudEventAttributes.DataBase64] = e.DataBase64;
        var splitHorizon = this.Configuration?.Resource.Spec.SplitHorizon;
        if (splitHorizon != null)
        {
            var origins = new List<string>();
            if (e.ExtensionAttributes?.TryGetValue(splitHorizon.OriginAttribute, out var origin) == true) origins = [.. origin.ToString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            origins.Add(this.Configuration!.Resource.GetQualifiedName());
            metadata.ContextAttributes[splitHorizon.OriginAttribute] = string.Join(' ', origins);
        }
        var ingestionConfiguration = this.Configuration?.Resource.Spec.Events?.LastOrDefault(c => c.AppliesTo(e));
        if (ingestionConfiguration == null || ingestionConfiguration.Metadata?.Properties == null) return metadata;
        foreach (var property in ingestionConfiguration.Metadata.Properties)
        {
            try
            {
                metadata.ExtensionData ??= new Dictionary<string, object>();
                switch (property.Strategy)
                {
                    case CloudEventMetadataPropertyResolutionStrategy.Attribute:
                        if (property.Attribute == null) throw new NullReferenceException($"The '{nameof(property.Attribute)}' property cannot be null when the metadata property resolution strategy has been set to '{EnumHelper.Stringify(CloudEventMetadataPropertyResolutionStrategy.Attribute)}'");
                        if (!e.TryGetAttribute(property.Attribute.Name, out var attributeValue))
                        {
                            this.Logger.LogWarning("Failed to extract the configured metadata property '{property}' from the cloud event with id '{eventId}': failed to find a context attribute with name '{attributeName}'", property.Name, e.Id, property.Attribute.Name);
                            continue;
                        }
                        if (!string.IsNullOrWhiteSpace(property.Attribute.Value) && !CloudStreamsRegex.IsMatch(attributeValue?.ToString() ?? string.Empty, property.Attribute.Value))
                        {
                            this.Logger.LogWarning("Failed to extract the configured metadata property '{property}' from the cloud event with id '{eventId}': the value of the context attribute with name '{attributeName}' does not match the configured pattern '{attributeValue}'", property.Name, e.Id, property.Attribute.Name, property.Attribute.Value);
                            continue;
                        }
                        metadata.ExtensionData![property.Name] = attributeValue!;
                        break;
                    case CloudEventMetadataPropertyResolutionStrategy.Expression:
                        if (string.IsNullOrWhiteSpace(property.Expression)) throw new NullReferenceException($"The '{nameof(property.Expression)}' property cannot be null when the metadata property resolution strategy has been set to '{EnumHelper.Stringify(CloudEventMetadataPropertyResolutionStrategy.Expression)}'");
                        attributeValue = await this.ExpressionEvaluator.EvaluateAsync(property.Expression, e, cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (attributeValue != null) metadata.ExtensionData![property.Name] = attributeValue;
                        break;
                    default:
                        throw new NotSupportedException($"The specified resolution strategy '{EnumHelper.Stringify(property.Strategy)}' is not supported");
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "An error occurred while extracting the metadata property '{property}' from the cloud event with id '{eventId}'", property.Name, e.Id);
                continue;
            }
        }
        return await Task.FromResult(metadata);
    }

    /// <summary>
    /// Disposes of the <see cref="CloudEventAdmissionControl"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="CloudEventAdmissionControl"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._disposed)
        {
            if (disposing) this.Configuration?.Dispose();
            this._disposed = true;
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

}
