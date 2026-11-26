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

using CloudStreams.Core.Transports;
using CloudStreams.Core.Api.Services;
using CloudStreams.Core.Application.Services;
using System.Text.Json;

namespace CloudStreams.Core.Api;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    /// <summary>
    /// Gets the options used to bind the arguments of MCP tools, and to serialize their results
    /// </summary>
    /// <remarks>
    /// The converters of the SDK's options take precedence over the converters the types declare: enums are read and written by their <see cref="System.Runtime.Serialization.EnumMemberAttribute"/>
    /// value instead, e.g. <c>by-source</c>, as they are everywhere else
    /// </remarks>
    public static JsonSerializerOptions McpSerializerOptions { get; } = CreateMcpSerializerOptions();

    static JsonSerializerOptions CreateMcpSerializerOptions()
    {
        var options = new JsonSerializerOptions(ModelContextProtocol.McpJsonUtilities.DefaultOptions);
        options.Converters.Insert(0, new Neuroglia.Serialization.Json.Converters.StringEnumConverter());
        options.MakeReadOnly();
        return options;
    }

    /// <summary>
    /// Configures CloudStreams to use the Core API
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseCoreApi(this ICloudStreamsApplicationBuilder builder)
    {
        builder.Services.AddSignalR();
        // redeliveries go through the transports, to endpoints and sinks, authenticated as configured (see #218)
        builder.Services.AddCloudStreamsTransports(builder.Configuration);
        builder.Services.AddSingleton<ResourceWatchEventHubController>();
        builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ResourceWatchEventHubController>());
        builder.Services.AddHostedService<GatewayResourceController>();
        builder.Services.AddHostedService<PartitionIndexInitializer>();
        builder.Services.AddHostedService<BrokerResourceController>();
        // agents connect with the Streamable HTTP transport, at /mcp. The legacy SSE transport, which has no backpressure, is not served
        builder.Services.AddMcpServer().WithToolsFromAssembly(serializerOptions: McpSerializerOptions).WithHttpTransport();
        builder.Services.AddHostedService<SecurityConfigurationAuditor>();
        return builder;
    }

}

