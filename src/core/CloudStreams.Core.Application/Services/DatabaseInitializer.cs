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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents the service used to initialize the resource database, and to seed it with the definitions of Cloud Streams resources
/// </summary>
/// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
/// <remarks>
/// Every Cloud Streams application initializes the database it shares with the others, and they usually start together. The base initializer only seeds a database
/// it has initialized itself, and fails when another application initialized it concurrently: one of the applications then either crashed on the conflict, or
/// started reconciling resources whose definitions did not exist yet, and crashed. Initialization is therefore made idempotent: a concurrent initialization is
/// tolerated, and every definition is ensured to exist, whichever application created it (see #204)
/// </remarks>
public class DatabaseInitializer(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
    : Neuroglia.Data.Infrastructure.ResourceOriented.Services.DatabaseInitializer(loggerFactory, serviceProvider)
{

    readonly ILogger _logger = loggerFactory.CreateLogger<DatabaseInitializer>();

    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var database = this.ServiceProvider.GetRequiredService<IDatabase>();
        try
        {
            await database.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ProblemDetailsException ex) when (IsConflict(ex))
        {
            this._logger.LogDebug("The database has been initialized concurrently by another application");
        }
        await this.SeedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var database = this.ServiceProvider.GetRequiredService<IDatabase>();
        foreach (var definition in CloudStreamsDefaults.Resources.Definitions.AsEnumerable())
        {
            try
            {
                await database.CreateResourceAsync(definition, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (ProblemDetailsException ex) when (IsConflict(ex))
            {
                this._logger.LogDebug("The definition of the resource '{definition}' already exists", definition.GetName());
            }
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="ProblemDetailsException"/> reports that a resource already exists
    /// </summary>
    /// <param name="ex">The <see cref="ProblemDetailsException"/> to check</param>
    /// <returns>A boolean indicating whether or not the specified <see cref="ProblemDetailsException"/> reports that a resource already exists</returns>
    /// <remarks>The resource database reports conflicts with a 400 status, titled 'Conflict'</remarks>
    static bool IsConflict(ProblemDetailsException ex) =>
        ex.Problem.Status == (int)HttpStatusCode.Conflict
        || string.Equals(ex.Problem.Title, "Conflict", StringComparison.OrdinalIgnoreCase)
        || ex.Problem.Detail?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true;

}
