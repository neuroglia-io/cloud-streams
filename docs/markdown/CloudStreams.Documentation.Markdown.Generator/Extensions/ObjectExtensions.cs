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

using CloudStreams.Core;
using CloudStreams.Core.Data.Models;
using Json.Schema;
using RandomNameGeneratorNG;
using System.Collections;

namespace CloudStreams.Documentation.Markdown.Generator;

public static class TypeExtensions
{

    public static object GetSample(this Type type)
    {
        var random = new Random();
        if (type.IsEnum) return GetEnumSample(type);
        if (type == typeof(bool)) return Convert.ToBoolean(random.Next(0, 2));
        if (type == typeof(short)) return (short)random.NextInt64(1000);
        if (type == typeof(int)) return random.Next(0, 1000);
        if (type == typeof(long)) return random.NextInt64(1000);
        if (type == typeof(double)) return (float)random.NextDouble();
        if (type == typeof(decimal)) return (decimal)random.NextDouble();
        if (type == typeof(float)) return (float)random.NextDouble();
        if (type == typeof(char)) return Convert.ToChar(random.Next(0, 255));
        if (type == typeof(Guid)) return Guid.NewGuid();
        if (type == typeof(TimeSpan)) return TimeSpan.FromDays(random.NextDouble());
        if (type == typeof(DateTime)) return DateTime.Now + TimeSpan.FromDays(random.NextDouble());
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.Now + TimeSpan.FromDays(random.NextDouble());
        if (type == typeof(Uri)) return new Uri($"https://dns.extension");
        if (type == typeof(string)) return new PlaceNameGenerator().GenerateRandomPlaceName();
        if (type == typeof(JsonSchema)) return GetJsonSchemaSample();
        if (typeof(Resource).IsAssignableFrom(type)) return GetResourceSample(type);
        if (type.GetGenericType(typeof(IDictionary<,>)) != null) return GetDictionarySample(type);
        if (type.IsEnumerable()) return GetArraySample(type);
        return GetComplexTypeSample(type);
    }

    static object GetEnumSample(Type type)
    {
        var values = Enum.GetValues(type).OfType<object>().ToArray();
        return values[new Random().Next(0, values.Length)];
    }

    static JsonSchema GetJsonSchemaSample(bool allowRecursiveGeneration = true)
    {
        var count = new Random().Next(1, 5);
        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object);
        if (allowRecursiveGeneration)
        {
            var properties = new Dictionary<string, JsonSchema>();
            for (int i = 0; i < count; i++)
            {
                properties.Add($"property{i + 1}", GetJsonSchemaSample(false));
            }
            builder = builder.Properties(properties);
        }
        return builder.Build();
    }

    static object GetResourceSample(Type type)
    {
        var metadata = new ResourceMetadata($"{type.Name.ToHyphenCase()}-{Guid.NewGuid().ToString().Split("-")[0]}");
        if (type == typeof(Resource)) return new Resource(new ResourceType("test.com", "v1", "custom-resources", "CustomResource"), metadata);
        var args = new List<object>(2) { metadata };
        var specType = type.GetGenericType(typeof(ISpec<>));
        if (specType != null) args.Add(specType.GetGenericArguments()[0].GetSample());
        return Activator.CreateInstance(type, args.ToArray())!;
    }

    static object GetDictionarySample(Type type)
    {
        var dictionaryType = type.GetGenericType(typeof(IDictionary<,>))!;
        var keyType = dictionaryType.GetGenericArguments()[0];
        var valueType = dictionaryType.GetGenericArguments()[1];
        if (type.IsInterface) dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var count = new Random().Next(1, 5);
        var dictionary = (IDictionary)Activator.CreateInstance(dictionaryType)!;
        for (int i = 0; i < count; i++)
        {
            var key = keyType.GetSample();
            if (key is string str) key = $"attribute{i + 1}";
            dictionary.Add(key, valueType.GetSample());
        }
        return dictionary;
    }

    static object GetArraySample(Type type)
    {
        var elementType = type.GetEnumerableElementType();
        var count = new Random().Next(1, 5);
        var elements = new List<object>(count);
        for (int i = 0; i < count; i++)
        {
            elements.Add(elementType.GetSample());
        }
        var json = Serializer.Json.Serialize(elements);
        return Serializer.Json.Deserialize(json, type)!;
    }

    static object GetComplexTypeSample(Type type)
    {
        var kvpType = type.GetGenericType(typeof(KeyValuePair<,>));
        if (kvpType != null)
        {
            var key = kvpType.GetGenericArguments()[0].GetSample();
            var value = kvpType.GetGenericArguments()[1].GetSample();
            return Activator.CreateInstance(kvpType, new object[] { key, value })!;
        }
        var instance = Activator.CreateInstance(type)!;
        foreach(var property in type.GetProperties().Where(p => p.CanRead && p.CanWrite))
        {
            property.SetValue(instance, property.PropertyType.GetSample());
        }
        return instance;
    }

}
