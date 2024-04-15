﻿// Copyright © 2024-Present The Cloud Streams Authors
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
/// Represents the service used to initialize the Cloud Streams resource database
/// </summary>
/// <inheritdoc/>
public class DatabaseInitializer(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
    : Neuroglia.Data.Infrastructure.ResourceOriented.Services.DatabaseInitializer(loggerFactory, serviceProvider)
{

    /// <inheritdoc/>
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var database = this.ServiceProvider.GetRequiredService<IDatabase>();
        foreach (var definition in CloudStreamsDefaults.Resources.Definitions.AsEnumerable())
        {
            await database.CreateResourceAsync(definition, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

}
