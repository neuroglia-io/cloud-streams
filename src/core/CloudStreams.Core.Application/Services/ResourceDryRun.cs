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

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents the service used to validate a change to a resource without performing it, which forms rely on before saving (see #260)
/// </summary>
/// <param name="validators">The services used to validate resources, as the admission control does before the repository performs a change</param>
/// <remarks>
/// The change is reviewed directly, rather than by asking the repository to perform a dry run, for two reasons: the repository persists resources created with a dry
/// run, and the admission control reports refusals as text, without the paths of the fields at fault, which forms need to show each error next to its field.
/// The validators are therefore called directly, and the problem of the first that refuses the change is returned as is
/// </remarks>
public class ResourceDryRun(IEnumerable<IResourceValidator> validators)
{

    /// <summary>
    /// Reviews the specified change
    /// </summary>
    /// <typeparam name="TResource">The type of the resource to change</typeparam>
    /// <param name="operation">The operation to review: create, replace or patch</param>
    /// <param name="name">The name of the resource</param>
    /// <param name="namespace">The namespace of the resource, if any</param>
    /// <param name="updated">The resource as changed, if known, i.e. for creations and replacements</param>
    /// <param name="original">The resource as it is, if any, i.e. for replacements and patches</param>
    /// <param name="patch">The patch to apply, for patches</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The reason why the change is refused, with the errors of each field, or null if it is admitted</returns>
    public virtual async Task<ProblemDetails?> ReviewAsync<TResource>(Operation operation, string name, string? @namespace, TResource? updated, TResource? original, Patch? patch, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        var definition = new TResource().Definition;
        var reference = new ResourceReference(new ResourceDefinitionReference(definition.Group, definition.Version, definition.Plural), name, @namespace);
        var request = new AdmissionReviewRequest(Guid.NewGuid().ToString("N"), operation, reference, null!, patch!, updated!, original!, null!, true);
        foreach (var validator in validators.Where(v => v.AppliesTo(operation, definition.Group, definition.Version, definition.Plural, @namespace)))
        {
            var response = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.Allowed) return response.Problem ?? new ProblemDetails(ProblemTypes.AdmissionFailed, "Admission Failed", (int)HttpStatusCode.BadRequest, $"The {definition.Kind.ToLowerInvariant()} '{name}' has been refused");
        }
        return null;
    }

    /// <summary>
    /// Converts the specified refusal into the result of an operation
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the operation</typeparam>
    /// <param name="problem">The refusal to convert</param>
    /// <returns>A new failed <see cref="OperationResult{T}"/></returns>
    public static OperationResult<TResult> Refused<TResult>(ProblemDetails problem) => new(problem.Status, errors: [new Error(problem.Type, problem.Title, problem.Status, problem.Detail, problem.Instance, problem.Errors)]);

}
