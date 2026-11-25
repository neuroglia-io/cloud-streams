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

using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Application.Services;
using Microsoft.Extensions.Options;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the service that applies the configured retention policy to the cloud event stream when the application starts (see #206)
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="options">The options used to configure the retention policy</param>
/// <param name="store">The service used to set the retention policy of the cloud event stream</param>
/// <remarks>Applying the policy is retried until it succeeds, since EventStoreDB may not be reachable yet</remarks>
public class StreamRetentionInitializer(ILogger<StreamRetentionInitializer> logger, IOptions<RetentionOptions> options, IStreamRetentionStore store)
    : BackgroundService
{

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var policy = options.Value.GetPolicy();
        if (policy == null) return;
        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                var current = await store.GetAsync(stoppingToken).ConfigureAwait(false);
                if (current == null ? policy.IsUnlimited : Same(current, policy)) return;
                await store.SetAsync(policy, stoppingToken).ConfigureAwait(false);
                if (policy.IsUnlimited) logger.LogInformation("Removed the retention policy of the cloud event stream: events are kept forever");
                else logger.LogInformation("Set the retention policy of the cloud event stream: events are kept {maxAge} and up to {maxCount}", policy.MaxAge?.ToTimeSpan() > TimeSpan.Zero ? $"for {policy.MaxAge}" : "of any age", policy.MaxCount > 0 ? $"{policy.MaxCount} of them" : "any amount of them");
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(5 * Math.Pow(2, attempt - 1), 300));
                logger.LogError(ex, "Failed to set the retention policy of the cloud event stream. Retrying in {delay}...", delay);
                try { await Task.Delay(delay, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    static bool Same(StreamRetention current, StreamRetention policy)
    {
        static TimeSpan Age(StreamRetention retention) => retention.MaxAge?.ToTimeSpan() ?? TimeSpan.Zero;
        return Age(current) == Age(policy) && (current.MaxCount ?? 0) == (policy.MaxCount ?? 0);
    }

}
