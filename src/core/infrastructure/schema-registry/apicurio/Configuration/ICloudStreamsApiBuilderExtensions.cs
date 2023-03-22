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

using CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Configuration;
using CloudStreams.Core.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Core.Infrastructure.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    private const string ConnectionStringName = "apicurio";

    /// <summary>
    /// Configures Cloud Streams to use the <see href="https://www.apicur.io/registry/">Apicurio Registry</see> implementation of the <see cref="ISchemaRegistry"/> interface
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseApicurioSchemaRegistry(this ICloudStreamsApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception($"Failed to find the '{ConnectionStringName}' connection string");
        builder.Services.AddApiCurioRegistryClient(options =>
        {
            options.ServerUri = new(connectionString);
            options.LineEndingFormatMode = LineEndingFormatMode.ConvertToUnix;
        });
        builder.Services.TryAddSingleton<ApiCurioSchemaRegistry>();
        builder.RegisterHealthCheck(healthChecks => healthChecks.AddUrlGroup(new Uri(connectionString), ConnectionStringName, tags: new string[] { "schema-registry" }));
        builder.UseSchemaRegistry<ApiCurioSchemaRegistry>();
        return builder;
    }

}
