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

using k8s.LeaderElection;
using k8s.LeaderElection.ResourceLock;

namespace CloudStreams.Operator.Services;

/// <summary>
/// Elects, with a Kubernetes lease, the single replica of the operator that reconciles workloads and issues certificates (see #279). Every replica serves
/// the admission webhook
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="kubernetes">The service used to interact with the Kubernetes API</param>
/// <param name="options">The options of the operator</param>
public class OperatorLeadership(ILogger<OperatorLeadership> logger, IKubernetes kubernetes, IOptions<OperatorOptions> options)
    : BackgroundService
{

    readonly Lock _lock = new();
    TaskCompletionSource<CancellationToken> _leadership = new(TaskCreationOptions.RunContinuationsAsynchronously);
    CancellationTokenSource? _term;

    /// <summary>
    /// Gets the identity of the replica, its pod's name
    /// </summary>
    public string Identity { get; } = Environment.GetEnvironmentVariable("POD_NAME") ?? $"{Environment.MachineName}-{Guid.NewGuid():N}"[..20];

    /// <summary>
    /// Gets a boolean indicating whether or not the replica currently leads
    /// </summary>
    public virtual bool IsLeader { get { lock (this._lock) return this._term != null; } }

    /// <summary>
    /// Waits until the replica leads
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A <see cref="CancellationToken"/> that is canceled when the replica stops leading</returns>
    public virtual async Task<CancellationToken> WaitForLeadershipAsync(CancellationToken cancellationToken)
    {
        Task<CancellationToken> leadership;
        lock (this._lock) leadership = this._leadership.Task;
        return await leadership.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var election = options.Value.LeaderElection;
        var @lock = new LeaseLock(kubernetes, options.Value.Namespace, election.LeaseName, this.Identity);
        using var elector = new LeaderElector(new LeaderElectionConfig(@lock)
        {
            LeaseDuration = election.LeaseDuration,
            RenewDeadline = election.RenewDeadline,
            RetryPeriod = election.RetryPeriod
        });
        elector.OnStartedLeading += this.OnStartedLeading;
        elector.OnStoppedLeading += this.OnStoppedLeading;
        elector.OnNewLeader += leader => logger.LogInformation("The replica '{leader}' leads the operator", leader);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await elector.RunUntilLeadershipLostAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "The election of the operator's leader failed. Retrying...");
                this.OnStoppedLeading();
                try { await Task.Delay(election.RetryPeriod, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
        this.OnStoppedLeading();
    }

    /// <summary>
    /// Handles the start of the replica's leadership
    /// </summary>
    protected virtual void OnStartedLeading()
    {
        lock (this._lock)
        {
            if (this._term != null) return;
            this._term = new();
            this._leadership.TrySetResult(this._term.Token);
        }
        logger.LogInformation("The replica '{identity}' now leads the operator: reconciling workloads", this.Identity);
    }

    /// <summary>
    /// Handles the end of the replica's leadership
    /// </summary>
    protected virtual void OnStoppedLeading()
    {
        CancellationTokenSource? term;
        lock (this._lock)
        {
            term = this._term;
            if (term == null) return;
            this._term = null;
            this._leadership = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        logger.LogWarning("The replica '{identity}' no longer leads the operator", this.Identity);
        term.Cancel();
        term.Dispose();
    }

}
