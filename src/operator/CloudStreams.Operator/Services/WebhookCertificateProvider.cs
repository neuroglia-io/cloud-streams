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

using k8s.Autorest;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CloudStreams.Operator.Services;

/// <summary>
/// Loads the serving certificate of the admission webhook from its secret, issued by the operator or by cert-manager, and reloads it when it is rotated (see #278).
/// Runs on every replica
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="kubernetes">The service used to interact with the Kubernetes API</param>
/// <param name="options">The options of the operator</param>
public class WebhookCertificateProvider(ILogger<WebhookCertificateProvider> logger, IKubernetes kubernetes, IOptions<OperatorOptions> options)
    : BackgroundService
{

    volatile X509Certificate2? _certificate;
    string? _version;

    /// <summary>
    /// Gets the current serving certificate, if it has been loaded
    /// </summary>
    public virtual X509Certificate2? Certificate => this._certificate;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var webhook = options.Value.Webhook;
        if (!webhook.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await this.LoadAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Failed to load the serving certificate of the admission webhook. Retrying..."); }
            // until it is loaded, it is retried every few seconds: the replica isn't ready meanwhile
            try { await Task.Delay(this._certificate == null ? TimeSpan.FromSeconds(3) : webhook.RefreshInterval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>
    /// Loads the serving certificate, if its secret changed
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task LoadAsync(CancellationToken cancellationToken)
    {
        V1Secret secret;
        try { secret = await kubernetes.CoreV1.ReadNamespacedSecretAsync(options.Value.Webhook.CertificateSecret, options.Value.Namespace, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInformation("Waiting for the serving certificate of the admission webhook, in the secret '{secret}'...", options.Value.Webhook.CertificateSecret);
            return;
        }
        if (secret.Metadata.ResourceVersion == this._version && this._certificate != null) return;
        if (secret.Data?.TryGetValue("tls.crt", out var certificate) != true || secret.Data?.TryGetValue("tls.key", out var key) != true || certificate == null || key == null)
        {
            logger.LogInformation("Waiting for the serving certificate of the admission webhook: the secret '{secret}' has no 'tls.crt' and 'tls.key' yet", options.Value.Webhook.CertificateSecret);
            return;
        }
        this._certificate = CertificateHelper.Load(Encoding.UTF8.GetString(certificate), Encoding.UTF8.GetString(key));
        this._version = secret.Metadata.ResourceVersion;
        logger.LogInformation("Loaded the serving certificate of the admission webhook, valid until {notAfter}", this._certificate.NotAfter);
    }

}
