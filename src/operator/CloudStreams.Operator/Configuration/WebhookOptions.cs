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

namespace CloudStreams.Operator.Configuration;

/// <summary>
/// Represents the options of the admission webhook the operator serves, which validates the resources written to Kubernetes directly (see #278)
/// </summary>
public class WebhookOptions
{

    /// <summary>
    /// Gets/sets a boolean indicating whether or not the operator serves the admission webhook
    /// </summary>
    public virtual bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets/sets the port the webhook is served on, over HTTPS
    /// </summary>
    public virtual int Port { get; set; } = 8443;

    /// <summary>
    /// Gets/sets the name of the service the API server calls the webhook with, in the operator's namespace, which the serving certificate is issued for
    /// </summary>
    public virtual string ServiceName { get; set; } = "cloud-streams-operator";

    /// <summary>
    /// Gets/sets the name of the ValidatingWebhookConfiguration whose CA bundle the operator sets, when it manages the certificates
    /// </summary>
    public virtual string ConfigurationName { get; set; } = "cloud-streams";

    /// <summary>
    /// Gets/sets the name of the secret of the operator's namespace that holds the serving certificate: 'tls.crt', 'tls.key' and 'ca.crt', as cert-manager
    /// writes them
    /// </summary>
    public virtual string CertificateSecret { get; set; } = "cloud-streams-operator-tls";

    /// <summary>
    /// Gets/sets a boolean indicating whether or not the operator issues and rotates the certificates itself, with a CA of its own. When false, they're
    /// expected from cert-manager, whose CA injector sets the CA bundle
    /// </summary>
    public virtual bool ManageCertificates { get; set; } = true;

    /// <summary>
    /// Gets/sets how long the serving certificates the operator issues are valid for
    /// </summary>
    public virtual TimeSpan CertificateValidity { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// Gets/sets how long before it expires a certificate is renewed
    /// </summary>
    public virtual TimeSpan RenewBefore { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets/sets the interval at which the replicas reload the serving certificate, and the leader checks whether it must be renewed
    /// </summary>
    public virtual TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(1);

}
