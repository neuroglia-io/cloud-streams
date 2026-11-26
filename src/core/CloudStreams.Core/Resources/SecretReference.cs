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

using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents a reference to a secret: either its name, which the configured providers are asked for in order, or the provider and the path to read it from
/// </summary>
/// <remarks>
/// <code>
/// use: orders-webhook                                   # the first configured provider that has it
/// use: { vault: cloud-streams/orders-webhook }          # an explicit provider and path
/// </code>
/// Each provider only ever reads what the broker or the gateway has been granted: Vault paths its policy allows, Kubernetes secrets labelled for Cloud Streams, files
/// under the configured directory, environment variables with the <c>CLOUDSTREAMS_SECRETS__</c> prefix
/// </remarks>
[JsonConverter(typeof(SecretReferenceJsonConverter))]
public record SecretReference
    : IYamlConvertible
{

    /// <summary>
    /// Initializes a new <see cref="SecretReference"/>
    /// </summary>
    public SecretReference() { }

    /// <summary>
    /// Initializes a new <see cref="SecretReference"/>
    /// </summary>
    /// <param name="name">The name of the secret, which the configured providers are asked for in order</param>
    public SecretReference(string name) => this.Name = name;

    /// <summary>
    /// Initializes a new <see cref="SecretReference"/>
    /// </summary>
    /// <param name="provider">The provider to read the secret from. See <see cref="SecretProviders"/></param>
    /// <param name="path">The path of the secret, within the provider</param>
    public SecretReference(string provider, string path)
    {
        this.Provider = provider;
        this.Name = path;
    }

    /// <summary>
    /// Gets/sets the name of the secret, or its path within its <see cref="Provider"/>
    /// </summary>
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the provider to read the secret from, if explicit. See <see cref="SecretProviders"/>
    /// </summary>
    public virtual string? Provider { get; set; }

    /// <inheritdoc/>
    public override string ToString() => this.Provider == null ? this.Name : $"{this.Provider}:{this.Name}";

    /// <summary>
    /// Gets the message that describes what a valid secret path is
    /// </summary>
    public const string PathRules = "it must be made of letters, digits, dashes, underscores and dots, in segments separated by slashes, none of which is '.' or '..'";

    /// <summary>
    /// Determines whether or not the specified secret path is valid
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>A boolean indicating whether or not the path is valid</returns>
    /// <remarks>Paths are resolved within the area of a namespace: segments such as '..' could otherwise designate the secrets of another namespace (see #215)</remarks>
    public static bool IsValidPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == "..") return false;
            if (!segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) return false;
        }
        return true;
    }

    void IYamlConvertible.Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            this.Name = scalar.Value;
            this.Provider = null;
            return;
        }
        parser.Consume<MappingStart>();
        var provider = parser.Consume<Scalar>().Value;
        var path = parser.Consume<Scalar>().Value;
        if (!parser.TryConsume<MappingEnd>(out _)) throw new YamlException("A secret reference names a single provider, e.g. { vault: cloud-streams/orders }");
        this.Provider = provider;
        this.Name = path;
    }

    void IYamlConvertible.Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        if (this.Provider == null)
        {
            emitter.Emit(new Scalar(this.Name));
            return;
        }
        emitter.Emit(new MappingStart(null, null, false, MappingStyle.Flow));
        emitter.Emit(new Scalar(this.Provider));
        emitter.Emit(new Scalar(this.Name));
        emitter.Emit(new MappingEnd());
    }

}

/// <summary>
/// Enumerates the providers secrets can be read from
/// </summary>
public static class SecretProviders
{

    /// <summary>
    /// Gets the provider that reads secrets from environment variables named <c>CLOUDSTREAMS_SECRETS__{NAME}__{KEY}</c>
    /// </summary>
    public const string Environment = "env";
    /// <summary>
    /// Gets the provider that reads secrets from files named <c>{directory}/{name}/{key}</c>, such as mounted Kubernetes secrets
    /// </summary>
    public const string File = "file";
    /// <summary>
    /// Gets the provider that reads Kubernetes secrets through the Kubernetes API
    /// </summary>
    public const string Kubernetes = "kubernetes";
    /// <summary>
    /// Gets the provider that reads secrets from the KV secrets engine of HashiCorp Vault
    /// </summary>
    public const string Vault = "vault";

    /// <summary>
    /// Gets all the providers
    /// </summary>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing every provider</returns>
    public static IEnumerable<string> AsEnumerable()
    {
        yield return Environment;
        yield return File;
        yield return Kubernetes;
        yield return Vault;
    }

}

/// <summary>
/// Represents the <see cref="JsonConverter"/> of <see cref="SecretReference"/>s, which are either a name or an object with a single property, the provider
/// </summary>
public class SecretReferenceJsonConverter
    : JsonConverter<SecretReference>
{

    /// <inheritdoc/>
    public override SecretReference? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return new(reader.GetString()!);
            case JsonTokenType.StartObject:
                reader.Read();
                if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("A secret reference names a single provider, e.g. { \"vault\": \"cloud-streams/orders\" }");
                var provider = reader.GetString()!;
                reader.Read();
                var path = reader.GetString() ?? throw new JsonException("The path of a secret must be a string");
                reader.Read();
                if (reader.TokenType != JsonTokenType.EndObject) throw new JsonException("A secret reference names a single provider, e.g. { \"vault\": \"cloud-streams/orders\" }");
                return new(provider, path);
            default:
                throw new JsonException($"Unexpected token '{reader.TokenType}' while reading a secret reference: expected the name of a secret, or an object naming its provider");
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SecretReference value, JsonSerializerOptions options)
    {
        if (value.Provider == null)
        {
            writer.WriteStringValue(value.Name);
            return;
        }
        writer.WriteStartObject();
        writer.WriteString(value.Provider, value.Name);
        writer.WriteEndObject();
    }

}
