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

using Hylo.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the <see cref="IPluginBootstrapper"/> implementation used to configure the <see cref="JQExpressionEvaluatorProvider"/> plugin
/// </summary>
public class JQExpressionEvaluatorPluginBootstrapper
    : IPluginBootstrapper
{

    const string ConnectionStringName = "eventstore";

    /// <summary>
    /// Initializes a new <see cref="JQExpressionEvaluatorPluginBootstrapper"/>
    /// </summary>
    /// <param name="applicationServices">The current application's services</param>
    public JQExpressionEvaluatorPluginBootstrapper(IServiceProvider applicationServices)
    {
        this.ApplicationServices = applicationServices;
    }

    /// <summary>
    /// Gets the current application's services
    /// </summary>
    protected IServiceProvider ApplicationServices { get; }

    /// <inheritdoc/>
    public virtual void ConfigureServices(IServiceCollection services)
    {
        var configuration = this.ApplicationServices.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception($"Failed to find the '{ConnectionStringName}' connection string");
        services.TryAddSingleton<JQExpressionEvaluator>();
    }



}
