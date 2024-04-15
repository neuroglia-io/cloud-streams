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
    protected IRepository Resources => this.ServiceProvider.GetRequiredService<IRepository>();

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
    protected IResourceMonitor<Core.Resources.Gateway>? Configuration { get; private set; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Core.Resources.Gateway? gateway = null;
        try
        {
            gateway = await this.Resources.GetAsync<Core.Resources.Gateway>(this.GatewayOptions.Name, this.GatewayOptions.Namespace, stoppingToken).ConfigureAwait(false);
        }
        catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { }
        finally
        {
            if (gateway == null) await this.Resources.AddAsync(new Core.Resources.Gateway(new ResourceMetadata(this.GatewayOptions.Name, this.GatewayOptions.Namespace), new GatewaySpec()), false, stoppingToken).ConfigureAwait(false);
            this.Configuration = await this.Resources.MonitorAsync<Core.Resources.Gateway>(this.GatewayOptions.Name, this.GatewayOptions.Namespace, false, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<OperationResult<CloudEventDescriptor>> EvaluateAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        var validationResults = new List<FluentValidation.Results.ValidationResult>(this.Validators.Count());
        foreach (var validator in this.Validators) validationResults.Add(await validator.ValidateAsync(e, cancellationToken).ConfigureAwait(false));
        if (!validationResults.All(r => r.IsValid))
        {
            return new((int)HttpStatusCode.BadRequest, errors: [new Error(ProblemTypes.SchemaValidationFailed, "Invalid", (int)HttpStatusCode.BadRequest, errors: validationResults
                .Where(r => !r.IsValid)
                .SelectMany(r => r.Errors)
                .GroupBy(e => e.PropertyName)
                .Select(g => new KeyValuePair<string, string[]>(g.Key, g.Select(e => e.ErrorMessage).ToArray())))]);
        }
        this.Logger.LogDebug("Started admission evaluation for cloud event with id '{eventId}'...", e.Id);
        var result = await this.AuthorizeAsync(e, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess())
        {
            this.Logger.LogDebug("Admission evaluation failed with status code '{statusCode}' for cloud event with id '{eventId}'", result.Status, e.Id);
            this._metrics.IncrementTotalRejectedEvents();
            return result.OfType<CloudEventDescriptor>();
        }
        result = await this.ValidateAsync(e, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess())
        {
            this.Logger.LogDebug("Admission evaluation failed with status code '{statusCode}' for cloud event with id '{eventId}'", result.Status, e.Id);
            this._metrics.IncrementTotalInvalidEvents();
            return result.OfType<CloudEventDescriptor>();
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
            schema = await this.SchemaRegistry.GetAsync(e.DataSchema, cancellationToken).ConfigureAwait(false);
            if (schema == null)
            {
                this.Logger.LogDebug("Validation of cloud event with id '{eventId}' failed: failed to find the specified data schema '{dataSchemaUri}'", e.Id, e.DataSchema);
                return new((int)HttpStatusCode.BadRequest);
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
        if (!e.Time.HasValue) e.Time = DateTimeOffset.Now;
        var metadata = new CloudEventMetadata(e.GetContextAttributes().ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
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
                        if (!string.IsNullOrWhiteSpace(property.Attribute.Value) && !Regex.IsMatch(attributeValue?.ToString()!, property.Attribute.Value))
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
                this.Logger.LogError("An error occurred while extracting the metadata property '{property}' from the cloud event with id '{eventId}': {error}", property.Name, e.Id, ex);
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
