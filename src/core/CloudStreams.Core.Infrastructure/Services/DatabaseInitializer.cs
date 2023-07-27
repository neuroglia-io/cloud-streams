// Copyright © 2023-Present The Cloud Streams Authors
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

using Hylo;
using Hylo.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace CloudStreams.Core.Infrastructure.Services;

/// <inheritdoc/>
public class DatabaseInitializer
    : Hylo.Infrastructure.Services.DatabaseInitializer
{

    /// <inheritdoc/>
    public DatabaseInitializer(ILoggerFactory loggerFactory, IDatabaseProvider databaseProvider) : base(loggerFactory, databaseProvider) { }

    /// <inheritdoc/>
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        await base.SeedAsync(cancellationToken).ConfigureAwait(false);
        await this.SeedResourceDefinitionsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Seeds the definitions of the resources used by CloudStreams
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task SeedResourceDefinitionsAsync(CancellationToken cancellationToken)
    {
        foreach(var definition in CloudStreamsDefaults.Resources.Definitions.AsEnumerable())
        {
            await this.DatabaseProvider.GetDatabase().CreateResourceAsync(definition, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

}
