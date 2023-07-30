// Copyright © 2023-Present The Cloud Streams Authors
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

using System.Net.Mime;

namespace CloudStreams.Core.Data;

/// <summary>
/// Represents a <see href="https://cloudevents.io/">Cloud Event</see> envelope
/// </summary>
[DataContract]
public record CloudEvent
    : IExtensible
{

    /// <summary>
    /// Gets/sets a string that uniquely identifies the cloud event in the scope of its source
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "id", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("id"), YamlMember(Order = 1, Alias = "id")]
    public virtual string Id { get; set; } = null!;

    /// <summary>
    /// Gets/sets the version of the CloudEvents specification which the event uses. Defaults to <see cref="CloudEventSpecVersion.v1"/>
    /// </summary>
    [DefaultValue(CloudEventSpecVersion.v1)]
    [DataMember(Order = 2, Name = "specversion", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("specversion"), YamlMember(Order = 2, Alias = "specversion")]
    public virtual string SpecVersion { get; set; } = CloudEventSpecVersion.v1;

    /// <summary>
    /// Gets/sets the date and time at which the event has been produced
    /// </summary>
    [DataMember(Order = 3, Name = "time"), JsonPropertyOrder(3), JsonPropertyName("time"), YamlMember(Order = 3, Alias = "time")]
    public virtual DateTimeOffset? Time { get; set; }

    /// <summary>
    /// Gets/sets the cloud event's type
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 4, Name = "source", IsRequired = true), JsonPropertyOrder(4), JsonPropertyName("source"), YamlMember(Order = 4, Alias = "source")]
    public virtual Uri Source { get; set; } = null!;

    /// <summary>
    /// Gets/sets the cloud event's type
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 5, Name = "type", IsRequired = true), JsonPropertyOrder(5), JsonPropertyName("type"), YamlMember(Order = 5, Alias = "type")]
    public virtual string Type { get; set; } = null!;

    /// <summary>
    /// Gets/sets a value that describes the subject of the event in the context of the event producer. Used as correlation id by default.
    /// </summary>
    [DataMember(Order = 6, Name = "subject"), JsonPropertyOrder(6), JsonPropertyName("subject"), YamlMember(Order = 6, Alias = "subject")]
    public virtual string? Subject { get; set; }

    /// <summary>
    /// Gets/sets the cloud event's data content type. Defaults to <see cref="MediaTypeNames.Application.Json"/>
    /// </summary>
    [DefaultValue(MediaTypeNames.Application.Json)]
    [DataMember(Order = 7, Name = "datacontenttype"), JsonPropertyOrder(7), JsonPropertyName("datacontenttype"), YamlMember(Order = 7, Alias = "datacontenttype")]
    public virtual string? DataContentType { get; set; } = MediaTypeNames.Application.Json;

    /// <summary>
    /// Gets/sets an <see cref="Uri"/> that references the versioned schema of the event's data
    /// </summary>
    [DataMember(Order = 8, Name = "dataschema"), JsonPropertyOrder(8), JsonPropertyName("dataschema"), YamlMember(Order = 8, Alias = "dataschema")]
    public virtual Uri? DataSchema { get; set; }

    /// <summary>
    /// Gets/sets the event's data, if any. Only used if the event has been formatted using the structured mode
    /// </summary>
    [DataMember(Order = 9, Name = "data"), JsonPropertyOrder(9), JsonPropertyName("data"), YamlMember(Order = 9, Alias = "data")]
    public virtual object? Data { get; set; }

    /// <summary>
    /// Gets/sets the event's binary data, encoded in base 64. Only used if the event has been formatted using the binary mode
    /// </summary>
    [DataMember(Order = 10, Name = "data_base64"), JsonPropertyOrder(10), JsonPropertyName("data_base64"), YamlMember(Order = 10, Alias = "data_base64")]
    public virtual string? DataBase64 { get; set; }

    /// <summary>
    /// Gets/sets an <see cref="IDictionary{TKey, TValue}"/> that contains the event's extension attributes
    /// </summary>
    [DataMember(Order = 11, Name = "extensionAttributes"), JsonExtensionData]
    public virtual IDictionary<string, object>? ExtensionAttributes { get; set; }

    /// <summary>
    /// Gets/sets the specified attribute
    /// </summary>
    /// <param name="attributeName">The name of the attribute to set</param>
    /// <returns>The attribute's value</returns>
    public virtual object? this[string attributeName] => this.GetAttribute(attributeName);

    IDictionary<string, object>? IExtensible.ExtensionData => this.ExtensionAttributes;

    /// <summary>
    /// Gets the specified attribute
    /// </summary>
    /// <param name="name">The name of the attribute to get</param>
    /// <returns>The value of the specified attribute</returns>
    public virtual object? GetAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        switch (name)
        {
            case CloudEventAttributes.Id: return this.Id;
            case CloudEventAttributes.SpecVersion: return this.SpecVersion;
            case CloudEventAttributes.Time: return this.Time;
            case CloudEventAttributes.Source: return this.Source;
            case CloudEventAttributes.Type: return this.Type;
            case CloudEventAttributes.Subject: return this.Subject;
            case CloudEventAttributes.DataContentType: return this.DataContentType;
            case CloudEventAttributes.DataSchema: return this.DataSchema;
            case CloudEventAttributes.Data: return this.Data;
            case CloudEventAttributes.DataBase64: return this.DataBase64;
            default:
                if (this.ExtensionAttributes?.TryGetValue(name, out var value) == true) return value;
                else return null;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => this.Id;

}