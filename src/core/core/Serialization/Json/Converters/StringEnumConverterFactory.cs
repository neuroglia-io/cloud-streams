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

using System.Collections.Concurrent;

namespace CloudStreams.Core.Serialization.Json.Converters;

/// <summary>
/// Represents the <see cref="JsonConverterFactory"/> used to create <see cref="StringEnumConverter{TEnum}"/>
/// </summary>
public class StringEnumConverterFactory
    : JsonConverterFactory
{

    /// <summary>
    /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> containing the mappings of types to their respective <see cref="JsonConverter"/>
    /// </summary>
    private static readonly ConcurrentDictionary<Type, JsonConverter> Converters = new();

    /// <inheritdoc/>
	public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum || (typeToConvert.IsGenericType && typeToConvert.IsNullable() && typeToConvert.GetGenericArguments().First().IsEnum);

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var enumType = typeToConvert;
        if (enumType.IsGenericType && enumType.IsNullable() && enumType.GetGenericArguments().First().IsEnum) enumType = enumType.GetGenericArguments().First();
        if (!Converters.TryGetValue(typeToConvert, out var converter) || converter == null)
        {
            var converterType = typeof(StringEnumConverter<>).MakeGenericType(typeToConvert);
            converter = (JsonConverter)Activator.CreateInstance(converterType, enumType)!;
            Converters.TryAdd(typeToConvert, converter);
        }
        return converter;
    }

}
