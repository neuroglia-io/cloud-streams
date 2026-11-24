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

using CloudStreams.Core.Application.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents the service that enforces the <see cref="OutboundRequestOptions"/> on the connections Cloud Streams opens on behalf of resources
/// </summary>
/// <remarks>
/// The policy is enforced when connecting, on the addresses the destination's host actually resolves to, so that neither a host name nor DNS rebinding can be used to
/// bypass it (see #148)
/// </remarks>
public class OutboundRequestPolicy
{

    static readonly System.Net.IPNetwork[] LinkLocalNetworks = [System.Net.IPNetwork.Parse("169.254.0.0/16"), System.Net.IPNetwork.Parse("fe80::/10")];

    /// <summary>
    /// Initializes a new <see cref="OutboundRequestPolicy"/>
    /// </summary>
    /// <param name="options">The service used to access the current <see cref="OutboundRequestOptions"/></param>
    public OutboundRequestPolicy(IOptions<OutboundRequestOptions> options)
    {
        this.Options = options.Value;
        this.DeniedNetworks = [.. this.Options.DeniedNetworks.Select(System.Net.IPNetwork.Parse)];
    }

    /// <summary>
    /// Gets the options used to configure the policy
    /// </summary>
    protected OutboundRequestOptions Options { get; }

    /// <summary>
    /// Gets the networks requests may not be sent to
    /// </summary>
    protected IReadOnlyList<System.Net.IPNetwork> DeniedNetworks { get; }

    /// <summary>
    /// Determines whether requests may be sent to the specified host, regardless of the addresses it resolves to
    /// </summary>
    /// <param name="host">The host to check</param>
    /// <returns>A boolean indicating whether or not requests may be sent to the specified host</returns>
    public virtual bool IsAllowed(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (this.Options.AllowedHosts.Count == 0) return true;
        return this.Options.AllowedHosts.Any(allowed => allowed.StartsWith("*.", StringComparison.Ordinal)
            ? host.EndsWith(allowed[1..], StringComparison.OrdinalIgnoreCase)
            : string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines whether requests may be sent to the specified address
    /// </summary>
    /// <param name="address">The address to check</param>
    /// <returns>A boolean indicating whether or not requests may be sent to the specified address</returns>
    public virtual bool IsAllowed(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (this.Options.DenyPrivateNetworks && !MemoryJsonSchemaRegistry.IsPublicAddress(address)) return false;
        if (this.Options.DenyLinkLocal && LinkLocalNetworks.Any(network => network.Contains(address))) return false;
        return !this.DeniedNetworks.Any(network => network.Contains(address));
    }

    /// <summary>
    /// Connects to the specified endpoint, refusing hosts and addresses the policy does not allow
    /// </summary>
    /// <param name="context">The context of the connection</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="Stream"/> of the connection</returns>
    /// <exception cref="HttpRequestException">The destination is not allowed, or could not be connected to</exception>
    public virtual async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        if (!this.IsAllowed(host)) throw new HttpRequestException($"Requests to '{host}' are not allowed: the host is not listed in '{OutboundRequestOptions.ConfigurationSection}:{nameof(OutboundRequestOptions.AllowedHosts)}'");
        var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        var allowed = addresses.Where(this.IsAllowed).ToArray();
        if (allowed.Length == 0) throw new HttpRequestException($"Requests to '{host}' are not allowed: it resolves to addresses denied by the '{OutboundRequestOptions.ConfigurationSection}' policy");
        Exception? lastError = null;
        foreach (var address in allowed)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                lastError = ex;
            }
        }
        throw new HttpRequestException($"Failed to connect to '{host}'", lastError);
    }

}
