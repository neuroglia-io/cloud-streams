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

using CloudStreams.Core.Application.Validation;

namespace CloudStreams.Core.Application;

/// <summary>
/// Defines extensions for <see cref="IResourceRepository"/> instances
/// </summary>
public static class IResourceRepositoryExtensions
{

    /// <summary>
    /// Creates the specified namespace, unless it already exists
    /// </summary>
    /// <param name="repository">The extended <see cref="IResourceRepository"/></param>
    /// <param name="namespace">The name of the namespace to ensure the existence of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// Gateways and brokers create the resource they serve when it does not exist: they create its namespace first, which would otherwise refuse it (see #215)
    /// </remarks>
    /// <exception cref="ArgumentException">The specified namespace name is not a DNS label</exception>
    public static async Task EnsureNamespaceAsync(this IResourceRepository repository, string @namespace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (!CloudStreamsResourceValidator.IsValidNamespaceName(@namespace)) throw new ArgumentException($"'{@namespace}' is not a valid namespace name: it must be at most 63 lowercase letters, digits and dashes, and start and end with a letter or a digit", nameof(@namespace));
        try { if (await repository.GetAsync<Namespace>(@namespace, null, cancellationToken).ConfigureAwait(false) != null) return; }
        catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { }
        try { await repository.AddAsync(new Namespace() { Metadata = new() { Name = @namespace } }, false, cancellationToken).ConfigureAwait(false); }
        // created by another process in the meantime, which the database reports as a conflict, whose status may be 400 (see #275)
        catch (ProblemDetailsException ex) when (ex.Problem.IsConflict()) { }
    }

}
