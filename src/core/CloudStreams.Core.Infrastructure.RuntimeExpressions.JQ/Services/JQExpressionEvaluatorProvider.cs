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

using Hylo.Infrastructure;
using Hylo.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the default JQ implementation of the <see cref="IExpressionEvaluatorProvider"/> interface
/// </summary>
[Plugin(typeof(IExpressionEvaluatorProvider), typeof(JQExpressionEvaluatorPluginBootstrapper))]
public class JQExpressionEvaluatorProvider
    : IExpressionEvaluatorProvider
{

    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="JQExpressionEvaluatorProvider"/>
    /// </summary>
    /// <param name="services">The current <see cref="IServiceProvider"/></param>
    public JQExpressionEvaluatorProvider(IServiceProvider services)
    {
        this.Services = services;
    }

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider Services { get; }

    /// <inheritdoc/>
    public virtual IExpressionEvaluatorProvider GetExpressionEvaluator() => this.Services.GetRequiredService<JQExpressionEvaluator>();

    /// <summary>
    /// Disposes of the <see cref="JQExpressionEvaluatorProvider"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="JQExpressionEvaluatorProvider"/> is being disposed of</param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!disposing || this._disposed) return;
        this._disposed = true;
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the <see cref="JQExpressionEvaluatorProvider"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="JQExpressionEvaluatorProvider"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || this._disposed) return;
        this._disposed = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

}
