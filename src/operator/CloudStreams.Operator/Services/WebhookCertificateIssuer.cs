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
/// Issues and rotates the certificates of the admission webhook, when the operator manages them rather than cert-manager (see #278). Only runs on the replica
/// that leads the operator
/// </summary>
/// <remarks>
/// The CA is valid for 10 years and the serving certificate for <see cref="WebhookOptions.CertificateValidity"/>. A serving certificate is renewed with the same CA,
/// so that the CA bundle the API server trusts doesn't change. A CA is replaced a year before it expires, and the bundle then holds both until the old one expires,
/// so that no replica serves a certificate the API server doesn't trust
/// </remarks>
/// <param name="logger">The service used to perform logging</param>
/// <param name="leadership">The service that elects the replica that leads the operator</param>
/// <param name="kubernetes">The service used to interact with the Kubernetes API</param>
/// <param name="options">The options of the operator</param>
public class WebhookCertificateIssuer(ILogger<WebhookCertificateIssuer> logger, OperatorLeadership leadership, IKubernetes kubernetes, IOptions<OperatorOptions> options)
    : LeaderService(logger, leadership)
{

    /// <summary>
    /// Gets the key of the CA's private key in the certificate's secret
    /// </summary>
    public const string CaKey = "ca.key";

    /// <summary>
    /// Gets the options of the operator
    /// </summary>
    protected OperatorOptions Options => options.Value;

    /// <inheritdoc/>
    protected override async Task RunAsLeaderAsync(CancellationToken cancellationToken)
    {
        if (!this.Options.Webhook.Enabled || !this.Options.Webhook.ManageCertificates) return;
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await this.IssueAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception ex) { this.Logger.LogError(ex, "Failed to issue the certificates of the admission webhook. Retrying..."); }
            await Task.Delay(this.Options.Webhook.RefreshInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Issues the certificates that are missing or expire soon, and sets the CA bundle of the webhook's configuration
    /// </summary>
    /// <param name="now">The current date and time</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task IssueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var webhook = this.Options.Webhook;
        V1Secret? secret = null;
        try { secret = await kubernetes.CoreV1.ReadNamespacedSecretAsync(webhook.CertificateSecret, this.Options.Namespace, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound) { }
        var data = secret?.Data ?? new Dictionary<string, byte[]>();
        string? Read(string key) => data.TryGetValue(key, out var value) && value != null ? Encoding.UTF8.GetString(value) : null;
        var bundle = CertificateHelper.ReadBundle(Read("ca.crt")).Where(c => c.NotAfter.ToUniversalTime() > now.UtcDateTime).ToList();
        X509Certificate2? ca = null;
        if (Read("ca.crt") is string caPem && Read(CaKey) is string caKeyPem)
        {
            try
            {
                var signing = CertificateHelper.ReadBundle(caPem).FirstOrDefault();
                if (signing != null) ca = X509Certificate2.CreateFromPem(signing.ExportCertificatePem(), caKeyPem);
            }
            catch (Exception ex) { this.Logger.LogWarning(ex, "The CA of the admission webhook could not be read: issuing a new one"); }
        }
        var changed = false;
        if (ca == null || CertificateHelper.MustRenew(ca, now, CertificateHelper.CaRenewBefore))
        {
            ca = CertificateHelper.CreateCa($"{this.Options.Instance}-operator-ca", now);
            // the new CA comes first: it signs the serving certificates. The previous ones remain trusted until they expire
            bundle.Insert(0, ca);
            changed = true;
            this.Logger.LogInformation("Issued a new CA for the admission webhook, valid until {notAfter}", ca.NotAfter);
        }
        X509Certificate2? serving = null;
        if (!changed && Read("tls.crt") is string servingPem && Read("tls.key") is string servingKeyPem)
        {
            try { serving = X509Certificate2.CreateFromPem(servingPem, servingKeyPem); }
            catch (Exception ex) { this.Logger.LogWarning(ex, "The serving certificate of the admission webhook could not be read: issuing a new one"); }
        }
        if (serving == null || CertificateHelper.MustRenew(serving, now, webhook.RenewBefore) || !IsSignedBy(serving, ca))
        {
            serving = CertificateHelper.CreateServingCertificate(ca, webhook.ServiceName, this.Options.Namespace, now, webhook.CertificateValidity);
            changed = true;
            this.Logger.LogInformation("Issued a new serving certificate for the admission webhook, valid until {notAfter}", serving.NotAfter);
        }
        var bundlePem = string.Concat(bundle.DistinctBy(c => c.Thumbprint).Select(CertificateHelper.ExportCertificate));
        if (changed)
        {
            var body = new V1Secret()
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = new() { Name = webhook.CertificateSecret, NamespaceProperty = this.Options.Namespace, Labels = new Dictionary<string, string>() { { "app.kubernetes.io/managed-by", WorkloadFactory.ManagedBy } } },
                Type = "kubernetes.io/tls",
                Data = new Dictionary<string, byte[]>()
                {
                    ["tls.crt"] = CertificateHelper.ToBytes(CertificateHelper.ExportCertificate(serving)),
                    ["tls.key"] = CertificateHelper.ToBytes(CertificateHelper.ExportPrivateKey(serving)),
                    ["ca.crt"] = CertificateHelper.ToBytes(bundlePem),
                    [CaKey] = CertificateHelper.ToBytes(CertificateHelper.ExportPrivateKey(ca))
                }
            };
            await kubernetes.CoreV1.PatchNamespacedSecretAsync(new V1Patch(body, V1Patch.PatchType.ApplyPatch), webhook.CertificateSecret, this.Options.Namespace, fieldManager: OperatorOptions.FieldManager, force: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        await this.SetCaBundleAsync(CertificateHelper.ToBytes(bundlePem), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the CA bundle of every webhook of the operator's ValidatingWebhookConfiguration, if it differs
    /// </summary>
    /// <param name="bundle">The CA bundle to set, as PEM</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task SetCaBundleAsync(byte[] bundle, CancellationToken cancellationToken)
    {
        V1ValidatingWebhookConfiguration configuration;
        try { configuration = await kubernetes.AdmissionregistrationV1.ReadValidatingWebhookConfigurationAsync(this.Options.Webhook.ConfigurationName, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            this.Logger.LogWarning("The ValidatingWebhookConfiguration '{name}' does not exist: resources applied with kubectl are not validated", this.Options.Webhook.ConfigurationName);
            return;
        }
        var operations = new JsonArray();
        for (var index = 0; index < (configuration.Webhooks?.Count ?? 0); index++)
        {
            if (configuration.Webhooks![index].ClientConfig?.CaBundle is byte[] current && current.SequenceEqual(bundle)) continue;
            operations.Add(new JsonObject()
            {
                ["op"] = configuration.Webhooks[index].ClientConfig?.CaBundle == null ? "add" : "replace",
                ["path"] = $"/webhooks/{index}/clientConfig/caBundle",
                ["value"] = Convert.ToBase64String(bundle)
            });
        }
        if (operations.Count < 1) return;
        operations.Insert(0, new JsonObject() { ["op"] = "test", ["path"] = "/metadata/resourceVersion", ["value"] = configuration.Metadata.ResourceVersion });
        await kubernetes.AdmissionregistrationV1.PatchValidatingWebhookConfigurationAsync(new V1Patch(operations, V1Patch.PatchType.JsonPatch), configuration.Metadata.Name, cancellationToken: cancellationToken).ConfigureAwait(false);
        this.Logger.LogInformation("Set the CA bundle of the ValidatingWebhookConfiguration '{name}'", configuration.Metadata.Name);
    }

    /// <summary>
    /// Determines whether the specified certificate is signed by the specified CA
    /// </summary>
    /// <param name="certificate">The certificate to check</param>
    /// <param name="ca">The CA to check</param>
    /// <returns>A boolean indicating whether the certificate is signed by the CA</returns>
    protected static bool IsSignedBy(X509Certificate2 certificate, X509Certificate2 ca)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(certificate);
    }

}
