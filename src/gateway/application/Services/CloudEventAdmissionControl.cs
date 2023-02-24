using CloudStreams.Core.Infrastructure;
using FluentValidation;
using System.Net;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Represents the default implementation of the <see cref="ICloudEventAdmissionControl"/> interface
/// </summary>
public class CloudEventAdmissionControl
    : BackgroundService, ICloudEventAdmissionControl, IDisposable
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="CloudEventAdmissionControl"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="gatewayOptions">The service used to access the current <see cref="Configuration.GatewayOptions"/></param>
    /// <param name="authorizationManager">The service used to manage authorization</param>
    /// <param name="schemaGenerator">The service used to generate <see cref="JsonSchema"/>s</param>
    /// <param name="schemaRegistry">The service used to manage <see cref="JsonSchema"/>s</param>
    /// <param name="resources">The service used to manage <see cref="IResource"/>s</param>
    /// <param name="validators">An <see cref="IEnumerable{T}"/> containing the <see cref="IValidator"/>s used to validate <see cref="CloudEvent"/>s</param>
    public CloudEventAdmissionControl(ILoggerFactory loggerFactory, IOptions<GatewayOptions> gatewayOptions, IAuthorizationManager authorizationManager, 
        ISchemaGenerator schemaGenerator, ISchemaRegistry schemaRegistry, IResourceRepository resources, IEnumerable<IValidator<CloudEvent>> validators)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.GatewayOptions = gatewayOptions.Value;
        this.AuthorizationManager = authorizationManager;
        this.SchemaGenerator = schemaGenerator;
        this.SchemaRegistry = schemaRegistry;
        this.Resources = resources;
        this.Validators = validators;
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// gets the object used to configure a 
    /// </summary>
    protected GatewayOptions GatewayOptions { get; }

    /// <summary>
    /// Gets the service used to manage authorization
    /// </summary>
    protected IAuthorizationManager AuthorizationManager { get; }

    /// <summary>
    /// Gets the service used to generate <see cref="JsonSchema"/>s
    /// </summary>
    protected ISchemaGenerator SchemaGenerator { get; }

    /// <summary>
    /// Gets the service used to manage <see cref="JsonSchema"/>s
    /// </summary>
    protected ISchemaRegistry SchemaRegistry { get; }

    /// <summary>
    /// Gets the service used to manage <see cref="IResource"/>s
    /// </summary>
    protected IResourceRepository Resources { get; }

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> containing the <see cref="IValidator"/>s used to validate <see cref="CloudEvent"/>s
    /// </summary>
    protected IEnumerable<IValidator<CloudEvent>> Validators { get; }

    /// <summary>
    /// Gets the service used to manage the state of the current <see cref="Core.Data.Models.Gateway"/>
    /// </summary>
    protected IResourceMonitor<Core.Data.Models.Gateway>? Configuration { get; private set; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.Configuration = await this.Resources.MonitorAsync<Core.Data.Models.Gateway>(this.GatewayOptions.Name, this.GatewayOptions.Namespace, stoppingToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual async Task<Response> EvaluateAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        var validationResults = new List<FluentValidation.Results.ValidationResult>(this.Validators.Count());
        foreach (var validator in this.Validators)
        {
            validationResults.Add(await validator.ValidateAsync(e, cancellationToken).ConfigureAwait(false));
        }
        if (!validationResults.All(r => r.IsValid)) return new()
        {
            Status = (int)HttpStatusCode.BadRequest,
            Title = ProblemTitles.ValidationFailed,
            Errors = validationResults
                .Where(r => !r.IsValid)
                .SelectMany(r => r.Errors)
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
        };
        this.Logger.LogDebug("Started admission evaluation for cloud event with id '{eventId}'...", e.Id);
        var result = await this.AuthorizeAsync(e, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccessStatusCode())
        {
            this.Logger.LogDebug("Admission evaluation failed with status code '{statusCode}' for cloud event with id '{eventId}': {detail}", result.Status, e.Id, result.Detail);
            return result;
        }
        result = await this.ValidateAsync(e, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccessStatusCode())
        {
            this.Logger.LogDebug("Admission evaluation failed with status code '{statusCode}' for cloud event with id '{eventId}': {detail}", result.Status, e.Id, result.Detail);
            return result;
        }
        this.Logger.LogDebug("Admission evaluation for cloud event with id '{eventId}' completed successfully", e.Id);
        return Response.Ok();
    }

    /// <summary>
    /// Authorizes the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to authorize</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A <see cref="Response"/> that describes the result of the operation</returns>
    protected virtual async Task<Response> AuthorizeAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        this.Logger.LogDebug("Authorizing cloud event with id '{eventId}'...", e.Id);
        var policy = this.Configuration?.Resource.Spec.Sources?.FirstOrDefault(s => s.Uri == e.Source)?.Authorization;
        if (policy == null) policy = this.Configuration?.Resource.Spec.Authorization;
        if (policy == null) return Response.Ok();
        var result = await this.AuthorizationManager.EvaluateAsync(e, policy, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccessStatusCode())
        {
            this.Logger.LogDebug("Authorization denied for cloud event with id '{eventId}': {detail}", e.Id, result.Detail);
            return result;
        }
        this.Logger.LogDebug("Authorization granted for cloud event with id '{eventId}' completed successfully", e.Id);
        return Response.Ok();
    }

    /// <summary>
    /// Validates the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to validate</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A <see cref="Response"/> that describes the result of the operation</returns>
    protected virtual async Task<Response> ValidateAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        this.Logger.LogDebug("Validating cloud event with id '{eventId}'...", e.Id);
        var policy = this.Configuration?.Resource.Spec.Sources?.FirstOrDefault(s => s.Uri == e.Source)?.Validation;
        if (policy == null) policy = this.Configuration?.Resource.Spec.Validation;
        if (policy == null) return Response.Ok();
        if (policy.SkipValidation)
        {
            this.Logger.LogDebug("Skipping validation of cloud event with id '{eventId}': the validation policy for source '{sourceUri}' is configured to skip validation", e.Id, e.Source);
            return Response.Ok();
        }
        var dataSchemaPolicy = policy.DataSchema;
        JsonSchema? schema = null;
        if (e.DataSchema == null)
        {
            if (dataSchemaPolicy == null) dataSchemaPolicy = this.Configuration?.Resource.Spec.Validation?.DataSchema;
            if (dataSchemaPolicy?.Required == true)
            {
                this.Logger.LogDebug("Validation of cloud event with id '{eventId}' failed: the validation policy for source '{sourceUri}' requires the cloud event's 'dataSchema' attribute to be set", e.Id, e.Source);
                return Response.ValidationFailed(StringExtensions.Format(ProblemDetails.MissingDataSchema, e.Source!));
            }
            var schemaUri = await this.SchemaRegistry.GetSchemaUriByIdAsync(e.Type, cancellationToken).ConfigureAwait(false);
            if(schemaUri != null)
            {
                schema = await this.SchemaRegistry.GetSchemaAsync(schemaUri, cancellationToken).ConfigureAwait(false);
                e.DataSchema = schemaUri;
            }
            else if(dataSchemaPolicy?.AutoGenerate == true && e.Data != null) 
            {
                schema = await this.SchemaGenerator.GenerateAsync(e.Data, new() { Id = e.Type }, cancellationToken).ConfigureAwait(false);
                schemaUri = await this.SchemaRegistry.RegisterSchemaAsync(schema!, cancellationToken).ConfigureAwait(false);
                e.DataSchema = schemaUri;
            }
        }
        else
        {
            schema = await this.SchemaRegistry.GetSchemaAsync(e.DataSchema, cancellationToken).ConfigureAwait(false);
            if (schema == null)
            {
                this.Logger.LogDebug("Validation of cloud event with id '{eventId}' failed: failed to find the specified data schema '{dataSchemaUri}'", e.Id, e.DataSchema);
                return Response.ValidationFailed(StringExtensions.Format(ProblemDetails.DataSchemaNotFound, e.DataSchema));
            }
        }
        if(schema != null)
        {
            var validationOptions = new ValidationOptions() { OutputFormat = OutputFormat.Detailed };
            var validationResults = schema.Validate(Serializer.Json.SerializeToNode(e.Data), validationOptions);
            if (!validationResults.IsValid)
            {
                this.Logger.LogDebug("Validation of cloud event with id '{eventId}' failed: {detail}", e.Id, validationResults.GetErrorMessage());
                return Response.ValidationFailed(validationResults);
            }
        }
        this.Logger.LogDebug("Cloud event with id '{eventId}' successfully validated", e.Id);
        return Response.Ok();
    }

    /// <summary>
    /// Disposes of the <see cref="CloudEventAdmissionControl"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="CloudEventAdmissionControl"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.Configuration?.Dispose();
            }
            this._Disposed = true;
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

}