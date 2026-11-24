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

using CloudStreams.Core.Resources;
using System.Text.Json;

namespace CloudStreams.Core.Serialization;

/// <summary>
/// Represents the <see cref="JsonConverter"/> used to serialize <see cref="Duration"/>s as ISO 8601 durations
/// </summary>
/// <remarks>Reads both ISO 8601 durations and the object form (e.g. <c>{ "seconds": 30 }</c>) produced by earlier versions</remarks>
public class DurationJsonConverter
    : JsonConverter<Duration>
{

    /// <inheritdoc/>
    public override Duration? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                var value = reader.GetString();
                if (Duration.TryParse(value, out var duration)) return duration;
                throw new JsonException($"The value '{value}' is not a valid, non-negative ISO 8601 duration, such as 'PT30S' or 'P1DT2H'");
            case JsonTokenType.StartObject:
                var result = new Duration();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected the name of a duration component");
                    var propertyName = reader.GetString();
                    reader.Read();
                    uint? component = reader.TokenType == JsonTokenType.Null ? null : reader.GetUInt32();
                    switch (propertyName?.ToLowerInvariant())
                    {
                        case "days": result.Days = component; break;
                        case "hours": result.Hours = component; break;
                        case "minutes": result.Minutes = component; break;
                        case "seconds": result.Seconds = component; break;
                        case "milliseconds": result.Milliseconds = component; break;
                        default: throw new JsonException($"Unknown duration component '{propertyName}'");
                    }
                }
                return result;
            default:
                throw new JsonException($"Unexpected token '{reader.TokenType}' while reading a duration: expected an ISO 8601 duration, such as 'PT30S'");
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Duration value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());

}
