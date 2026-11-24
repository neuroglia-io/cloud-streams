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
using FluentValidation.Results;
using Neuroglia.Data.PatchModel.Services;
using Neuroglia.Serialization;

namespace CloudStreams.Core.Application.Validation;

/// <summary>
/// Represents the <see cref="IResourceValidator"/> that validates the specs of Cloud Streams resources when they are created, replaced or patched
/// </summary>
/// <param name="serializer">The service used to serialize/deserialize objects to/from JSON</param>
/// <param name="patchHandlers">The services used to apply patches, to validate the state that results from a patch</param>
/// <remarks>
/// Status updates are not validated: they are made by Cloud Streams itself, and validating them could prevent a broker from recording the state of a subscription
/// whose spec has become invalid (see #170)
/// </remarks>
public class CloudStreamsResourceValidator(IJsonSerializer serializer, IEnumerable<IPatchHandler> patchHandlers)
    : IResourceValidator
{

    /// <inheritdoc/>
    public virtual bool AppliesTo(Operation operation, string group, string version, string plural, string? @namespace = null) =>
        operation is Operation.Create or Operation.Replace or Operation.Patch
        && group == CloudStreamsDefaults.ResourceGroup
        && plural is "subscriptions" or "brokers" or "gateways";

    /// <inheritdoc/>
    public virtual async Task<AdmissionReviewResponse> ValidateAsync(AdmissionReviewRequest context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.IsNullOrWhiteSpace(context.SubResource)) return new(context.Uid, true);
        var state = await this.GetUpdatedStateAsync(context, cancellationToken).ConfigureAwait(false);
        if (state == null) return new(context.Uid, true);
        var result = context.Resource.Definition.Plural switch
        {
            "subscriptions" => await new SubscriptionValidator().ValidateAsync(this.Convert<Subscription>(state), cancellationToken).ConfigureAwait(false),
            "brokers" => await new BrokerValidator().ValidateAsync(this.Convert<Broker>(state), cancellationToken).ConfigureAwait(false),
            "gateways" => await new GatewayValidator().ValidateAsync(this.Convert<Gateway>(state), cancellationToken).ConfigureAwait(false),
            _ => new ValidationResult()
        };
        if (result.IsValid) return new(context.Uid, true);
        var errors = result.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        var problem = new ProblemDetails(ProblemTypes.AdmissionFailed, "Admission Failed", (int)HttpStatusCode.BadRequest,
            $"The {context.Resource.Definition.Plural.TrimEnd('s')} '{context.Resource.Name}' is invalid: {string.Join("; ", errors.SelectMany(e => e.Value))}", null, errors);
        return new(context.Uid, false, null, problem);
    }

    /// <summary>
    /// Gets the state of the resource once the reviewed operation has been performed
    /// </summary>
    /// <param name="context">The <see cref="AdmissionReviewRequest"/> to get the updated state of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The updated state, if any</returns>
    /// <remarks>Patch requests carry the patch and the original state, but not always the state they result in, which is then computed here</remarks>
    protected virtual async Task<object?> GetUpdatedStateAsync(AdmissionReviewRequest context, CancellationToken cancellationToken)
    {
        if (context.UpdatedState != null || context.Operation != Operation.Patch || context.Patch == null || context.OriginalState == null) return context.UpdatedState;
        var handler = patchHandlers.FirstOrDefault(h => h.Supports(context.Patch.Type));
        if (handler == null) return null;
        var original = this.Convert<Resource>(context.OriginalState);
        return await handler.ApplyPatchAsync(context.Patch.Document, original, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Converts the specified resource into a resource of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of resource to convert to</typeparam>
    /// <param name="resource">The resource to convert</param>
    /// <returns>The converted resource</returns>
    protected virtual TResource Convert<TResource>(object resource)
        where TResource : class, IResource
        => resource as TResource ?? serializer.Deserialize<TResource>(serializer.SerializeToText(resource))!;

}
