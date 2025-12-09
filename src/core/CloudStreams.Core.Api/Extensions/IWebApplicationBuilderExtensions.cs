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

using CloudStreams.Core.Api.Services;
using CloudStreams.Core.Application.Services;

namespace CloudStreams.Core.Api;

/// <summary>
/// Defines extensions for <see cref="WebApplicationBuilder"/>s
/// </summary>
public static class IWebApplicationBuilderExtensions
{

    /// <summary>
    /// Configures the <see cref="WebApplicationBuilder"/> to use Cloud Streams
    /// </summary>
    /// <param name="app">The <see cref="WebApplicationBuilder"/> to configure</param>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup the <see cref="ICloudStreamsApplicationBuilder"/></param>
    /// <returns>The configured <see cref="WebApplicationBuilder"/></returns>
    public static WebApplicationBuilder UseCloudStreams(this WebApplicationBuilder app, Action<ICloudStreamsApplicationBuilder> setup)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(setup);
        var builder = new CloudStreamsApplicationBuilder(app.Configuration, app.Environment, app.Services, app.Logging);
        setup(builder);
        builder.Build();
        return app;
    }

}

