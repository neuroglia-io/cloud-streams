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

namespace CloudStreams.Core.Transports;

/// <summary>
/// Represents the options used to configure the transports a broker delivers cloud events over, or a gateway consumes cloud events from (see #218)
/// </summary>
public class TransportOptions
{

    /// <summary>
    /// Gets the path of the configuration section that holds the transport options
    /// </summary>
    public const string ConfigurationSection = "CloudStreams:Transports";

    /// <summary>
    /// Gets/sets the credentials that addresses can use, by name. See <see cref="TransportAddress.Credentials"/>
    /// </summary>
    /// <remarks>
    /// Credentials are configured on the process rather than in resources, which anyone allowed to read them could see, e.g. with the
    /// <c>CLOUDSTREAMS__TRANSPORTS__CREDENTIALS__ORDERS__PASSWORD</c> environment variable, which may come from a Kubernetes secret
    /// </remarks>
    public virtual Dictionary<string, TransportCredentials> Credentials { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the credentials of the specified address, if it names any
    /// </summary>
    /// <param name="address">The address to get the credentials of</param>
    /// <returns>The credentials of the address, or null if it names none</returns>
    /// <exception cref="InvalidOperationException">The address names credentials that are not configured</exception>
    public virtual TransportCredentials? GetCredentials(TransportAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.Credentials == null) return null;
        return this.Credentials.TryGetValue(address.Credentials, out var credentials) ? credentials
            : throw new InvalidOperationException($"The credentials '{address.Credentials}' that '{address.Uri}' names are not configured: set them under '{ConfigurationSection}:Credentials:{address.Credentials}', e.g. with the 'CLOUDSTREAMS__TRANSPORTS__CREDENTIALS__{address.Credentials.ToUpperInvariant()}__PASSWORD' environment variable");
    }

}

/// <summary>
/// Represents the credentials used to connect to a transport
/// </summary>
public class TransportCredentials
{

    /// <summary>
    /// Gets/sets the user name: the SASL user name with Kafka, the user name with NATS and AMQP
    /// </summary>
    public virtual string? Username { get; set; }

    /// <summary>
    /// Gets/sets the password that goes with the <see cref="Username"/>
    /// </summary>
    public virtual string? Password { get; set; }

    /// <summary>
    /// Gets/sets a token: the authentication token with NATS, a bearer token with another Cloud Streams gateway
    /// </summary>
    public virtual string? Token { get; set; }

    /// <summary>
    /// Gets/sets the API key of another Cloud Streams gateway
    /// </summary>
    public virtual string? ApiKey { get; set; }

    /// <summary>
    /// Gets/sets the name of the header that conveys the <see cref="ApiKey"/>. Defaults to <c>X-Api-Key</c>
    /// </summary>
    public virtual string? ApiKeyHeader { get; set; }

    /// <summary>
    /// Gets/sets the path of a NATS credentials file, which holds a user JWT and its seed
    /// </summary>
    public virtual string? CredentialsFile { get; set; }

}
