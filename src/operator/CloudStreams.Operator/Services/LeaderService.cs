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

namespace CloudStreams.Operator.Services;

/// <summary>
/// Represents the base class of the services that only run on the replica that leads the operator
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="leadership">The service that elects the replica that leads the operator</param>
public abstract class LeaderService(ILogger logger, OperatorLeadership leadership)
    : BackgroundService
{

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; } = logger;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            CancellationToken term;
            try { term = await leadership.WaitForLeadershipAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(term, stoppingToken);
            try
            {
                await this.RunAsLeaderAsync(cancellation.Token).ConfigureAwait(false);
                // a service whose work is done runs again in the next term only, rather than over and over in this one
                await Task.Delay(Timeout.Infinite, cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "The leader's service '{service}' failed. Restarting...", this.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>
    /// Runs the service until the replica stops leading
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that is canceled when the replica stops leading, or stops</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected abstract Task RunAsLeaderAsync(CancellationToken cancellationToken);

}
