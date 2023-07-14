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

using Microsoft.Extensions.Caching.Memory;
using System.Reflection;

namespace CloudStreams;

/// <summary>
/// Acts as an helper to find a filter types
/// </summary>
public static class TypeCacheUtil
{

    private static readonly MemoryCache Cache = new(new MemoryCacheOptions() { });

    /// <summary>
    /// Find types filtered by a given predicate
    /// </summary>
    /// <param name="cacheKey">The cache key used to store the results</param>
    /// <param name="predicate">The predicate that filters the types</param>
    /// <param name="assemblies">An array containing the assemblies to scan</param>
    /// <returns>The filtered types</returns>
    public static IEnumerable<Type> FindFilteredTypes(string cacheKey, Func<Type, bool> predicate, params Assembly[] assemblies)
    {
        if (Cache.TryGetValue(cacheKey, out var cachedValue) && cachedValue != null) return (IEnumerable<Type>)cachedValue;
        var types = assemblies.ToList().SelectMany(a =>
        {
            try
            {
                return a.DefinedTypes;
            }
            catch
            {
                return Array.Empty<Type>().AsEnumerable();
            }
        });
        var result = new List<Type>(types.Count());
        foreach (Type type in types)
        {
            if (predicate(type))
            {
                result.Add(type);
            }
        }
        return result;
    }

    /// <summary>
    /// Find types filtered by a given predicate
    /// </summary>
    /// <param name="cacheKey">The cache key used to store the results</param>
    /// <param name="predicate">The predicate that filters the types</param>
    /// <returns>The filtered types</returns>
    public static IEnumerable<Type> FindFilteredTypes(string cacheKey, Func<Type, bool> predicate) => FindFilteredTypes(cacheKey, predicate, AssemblyLocator.GetAssemblies().ToArray());

}