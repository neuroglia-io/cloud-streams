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

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="Object"/>s
/// </summary>
public static class ObjectExtensions
{

    /// <summary>
    /// Clones the object
    /// </summary>
    /// <param name="obj">The object to clone</param>
    /// <returns>The clone</returns>
    public static object? Clone(this object? obj) => Serializer.Json.Deserialize<object>(Serializer.Json.Serialize(obj));

    /// <summary>
    /// Clones the object
    /// </summary>
    /// <typeparam name="T">The type of the object to clone</typeparam>
    /// <param name="obj">The object to clone</param>
    /// <returns>The clone</returns>
    public static T? Clone<T>(this T? obj) => Serializer.Json.Deserialize<T>(Serializer.Json.Serialize(obj));

    /// <summary>
    /// Converts the object into a new <see cref="Dictionary{TKey, TValue}"/>
    /// </summary>
    /// <param name="obj">The object to convert</param>
    /// <returns>A new <see cref="Dictionary{TKey, TValue}"/></returns>
    public static Dictionary<string, object>? ToDictionary(this object? obj) => Serializer.Json.Deserialize<Dictionary<string, object>>(Serializer.Json.Serialize(obj));

    /// <summary>
    /// Converts the object into a new <see cref="Dictionary{TKey, TValue}"/>
    /// </summary>
    /// <param name="obj">The object to convert</param>
    /// <returns>A new <see cref="Dictionary{TKey, TValue}"/></returns>
    public static Dictionary<string, TValue>? ToDictionary<TValue>(this object? obj) => Serializer.Json.Deserialize<Dictionary<string, TValue>>(Serializer.Json.Serialize(obj));

}