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
using Json.Schema;
using Microsoft.Extensions.Options;
using Neuroglia.Serialization;
using System.Net;
using System.Net.Sockets;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents the in-memory implementation of the <see cref="IJsonSchemaRegistry"/> interface
/// </summary>
/// <remarks>
/// <para>
/// Schema URIs come from the <c>dataschema</c> attribute of published cloud events, so fetching them must not let publishers reach the gateway's internal network
/// (see #141). Only <c>http</c> and <c>https</c> URIs are fetched, from the configured hosts if any, and connections to private, loopback and link-local addresses are
/// refused unless explicitly allowed. The check is made on the address actually connected to, so that DNS rebinding cannot bypass it.
/// </para>
/// <para>
/// Fetching is bounded by a timeout and a maximum size, and schemas are kept in a bounded cache rather than in the process-wide schema registry, whose size publishers
/// could otherwise grow without limit.
/// </para>
/// </remarks>
public class MemoryJsonSchemaRegistry
    : IJsonSchemaRegistry, IDisposable
{

    readonly Lock _lock = new();
    readonly Dictionary<Uri, LinkedListNode<(Uri Uri, JsonSchema Schema)>> _entries = [];
    readonly LinkedList<(Uri Uri, JsonSchema Schema)> _recency = new();
    bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="MemoryJsonSchemaRegistry"/>
    /// </summary>
    /// <param name="options">The service used to access the current <see cref="JsonSchemaRegistryOptions"/></param>
    /// <param name="serializer">The service used to serialize/deserialize objects to/from JSON</param>
    public MemoryJsonSchemaRegistry(IOptions<JsonSchemaRegistryOptions> options, IJsonSerializer serializer)
    {
        this.Options = options.Value;
        this.Serializer = serializer;
        var handler = new SocketsHttpHandler()
        {
            AllowAutoRedirect = false,
            ConnectCallback = this.ConnectAsync
        };
        this.HttpClient = new HttpClient(handler) { Timeout = this.Options.Timeout };
    }

    /// <summary>
    /// Gets the options used to configure the registry
    /// </summary>
    protected JsonSchemaRegistryOptions Options { get; }

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from JSON
    /// </summary>
    protected IJsonSerializer Serializer { get; }

    /// <summary>
    /// Gets the service used to fetch schemas
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <inheritdoc/>
    /// <exception cref="UnauthorizedAccessException">The schema's URI is not allowed</exception>
    /// <exception cref="InvalidDataException">The schema could not be fetched or parsed</exception>
    public virtual async Task<JsonSchema> GetAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        lock (this._lock)
        {
            if (this._entries.TryGetValue(uri, out var node))
            {
                this._recency.Remove(node);
                this._recency.AddFirst(node);
                return node.Value.Schema;
            }
        }
        if (!this.IsAllowed(uri)) throw new UnauthorizedAccessException($"Fetching schemas from '{uri}' is not allowed. Schemas must be served over HTTP(S), by one of the hosts configured in '{JsonSchemaRegistryOptions.ConfigurationSectionName}:{nameof(JsonSchemaRegistryOptions.AllowedHosts)}', if any");
        string json;
        try
        {
            using var response = await this.HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > this.Options.MaxSize) throw new InvalidDataException($"The schema exceeds the maximum size of {this.Options.MaxSize} bytes");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > this.Options.MaxSize) throw new InvalidDataException($"The schema exceeds the maximum size of {this.Options.MaxSize} bytes");
                buffer.Write(chunk, 0, read);
            }
            json = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new InvalidDataException($"Failed to fetch the schema '{uri}': {ex.Message}", ex);
        }
        JsonSchema schema;
        try { schema = this.Serializer.Deserialize<JsonSchema>(json) ?? throw new InvalidDataException("The document is empty"); }
        catch (Exception ex) when (ex is not InvalidDataException) { throw new InvalidDataException($"The document at '{uri}' is not a valid JSON schema: {ex.Message}", ex); }
        this.Add(uri, schema);
        return schema;
    }

    /// <inheritdoc/>
    public virtual Task RegisterAsync(JsonSchema schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        this.Add(schema.BaseUri, schema);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds the specified schema to the cache, evicting the least recently used one if the cache is full
    /// </summary>
    /// <param name="uri">The URI of the schema to add</param>
    /// <param name="schema">The schema to add</param>
    protected virtual void Add(Uri uri, JsonSchema schema)
    {
        lock (this._lock)
        {
            if (this._entries.Remove(uri, out var existing)) this._recency.Remove(existing);
            this._entries[uri] = this._recency.AddFirst((uri, schema));
            while (this._entries.Count > Math.Max(1, this.Options.CacheSize))
            {
                var last = this._recency.Last!;
                this._recency.RemoveLast();
                this._entries.Remove(last.Value.Uri);
            }
        }
    }

    /// <summary>
    /// Determines whether schemas may be fetched from the specified URI, regardless of the address its host resolves to
    /// </summary>
    /// <param name="uri">The URI to check</param>
    /// <returns>A boolean indicating whether or not schemas may be fetched from the specified URI</returns>
    public virtual bool IsAllowed(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return false;
        if (this.Options.AllowedHosts.Count == 0) return true;
        var host = uri.IdnHost;
        return this.Options.AllowedHosts.Any(allowed => allowed.StartsWith("*.", StringComparison.Ordinal)
            ? host.EndsWith(allowed[1..], StringComparison.OrdinalIgnoreCase)
            : string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Connects to the specified endpoint, refusing addresses that are not allowed
    /// </summary>
    /// <param name="context">The context of the connection</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="Stream"/> of the connection</returns>
    protected virtual async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);
        var allowed = this.Options.AllowPrivateNetworks ? addresses : [.. addresses.Where(IsPublicAddress)];
        if (allowed.Length == 0) throw new HttpRequestException($"The host '{context.DnsEndPoint.Host}' resolves to private addresses, from which schemas may not be fetched");
        Exception? lastError = null;
        foreach (var address in allowed)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException)
            {
                socket.Dispose();
                lastError = ex;
            }
        }
        throw new HttpRequestException($"Failed to connect to '{context.DnsEndPoint.Host}'", lastError);
    }

    /// <summary>
    /// Determines whether the specified address is a public one, as opposed to a private, loopback, link-local, multicast or otherwise reserved one
    /// </summary>
    /// <param name="address">The address to check</param>
    /// <returns>A boolean indicating whether or not the specified address is public</returns>
    public static bool IsPublicAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.Broadcast)) return false;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal) return false;
            return true;
        }
        var bytes = address.GetAddressBytes();
        return bytes[0] switch
        {
            0 or 10 or 127 => false,
            100 when bytes[1] >= 64 && bytes[1] <= 127 => false,
            169 when bytes[1] == 254 => false,
            172 when bytes[1] >= 16 && bytes[1] <= 31 => false,
            192 when bytes[1] == 168 => false,
            >= 224 => false,
            _ => true
        };
    }

    /// <summary>
    /// Disposes of the <see cref="MemoryJsonSchemaRegistry"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="MemoryJsonSchemaRegistry"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (this._disposed) return;
        if (disposing) this.HttpClient.Dispose();
        this._disposed = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

}
