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

using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace CloudStreams.Core.Serialization.Yaml;

/// <summary>
/// Represents the <see cref="INodeDeserializer"/> used to deserialize <see cref="JsonObject"/>s
/// </summary>
public class JsonObjectDeserializer
    : INodeDeserializer
{

    /// <summary>
    /// Initializes a new <see cref="JsonObjectDeserializer"/>
    /// </summary>
    /// <param name="inner">The inner <see cref="INodeDeserializer"/></param>
    public JsonObjectDeserializer(INodeDeserializer inner)
    {
        this.Inner = inner;
    }

    /// <summary>
    /// Gets the inner <see cref="INodeDeserializer"/>
    /// </summary>
    protected INodeDeserializer Inner { get; }

    /// <inheritdoc/>
    public virtual bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object? value)
    {
        if (!typeof(JsonObject).IsAssignableFrom(expectedType)) return this.Inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value);
        if (!this.Inner.Deserialize(reader, typeof(Dictionary<object, object>), nestedObjectDeserializer, out value)) return false;
        value = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(value));
        return true;
    }

}
