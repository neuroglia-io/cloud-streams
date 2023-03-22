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

using System.Collections;
using System.Reflection;

namespace CloudStreams.Core.EnumerableExtensions;

/// <summary>
/// Defines extensions for <see cref="IEnumerable"/>s
/// </summary>
public static class IEnumerableExtensions
{

    static readonly MethodInfo CountMethod = typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Count));
    static readonly MethodInfo OfTypeMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.OfType))!;
    static readonly MethodInfo ToArrayMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray))!;
    static readonly MethodInfo ToListMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!;

    /// <summary>
    /// Counts the amount of elements in the <see cref="IEnumerable"/>
    /// </summary>
    /// <param name="enumerable">The <see cref="IEnumerable"/> to count</param>
    /// <returns>The amount of elements in the <see cref="IEnumerable"/></returns>
    public static int Count(this IEnumerable enumerable) => (int)CountMethod.MakeGenericMethod(enumerable.GetType().GetEnumerableElementType()).Invoke(null, new object[] { enumerable })!;

    /// <summary>
    /// Filters the elements of the <see cref="IEnumerable"/> by type
    /// </summary>
    /// <param name="enumerable">The <see cref="IEnumerable"/> to filter</param>
    /// <param name="type">The type to filter the <see cref="IEnumerable"/> by</param>
    /// <returns>The filtered <see cref="IEnumerable"/></returns>
    public static IEnumerable OfType(this IEnumerable enumerable, Type type) => (IEnumerable)OfTypeMethod.MakeGenericMethod(type).Invoke(null, new object[] { enumerable })!;

    /// <summary>
    /// Converts the <see cref="IEnumerable"/> into a new array
    /// </summary>
    /// <param name="enumerable">The <see cref="IEnumerable"/> to convert</param>
    /// <returns>A new array, made out of the <see cref="IEnumerable"/>'s values</returns>
    public static IEnumerable ToArray(this IEnumerable enumerable) => (IEnumerable)ToArrayMethod.MakeGenericMethod(enumerable.GetType().GetEnumerableElementType()).Invoke(null, new object[] { enumerable })!;

    /// <summary>
    /// Converts the <see cref="IEnumerable"/> into a new <see cref="IList"/>
    /// </summary>
    /// <param name="enumerable">The <see cref="IEnumerable"/> to convert</param>
    /// <returns>A new <see cref="IList"/>, made out of the <see cref="IEnumerable"/>'s values</returns>
    public static IList ToList(this IEnumerable enumerable) => (IList)ToListMethod.MakeGenericMethod(enumerable.GetType().GetEnumerableElementType()).Invoke(null, new object[] { enumerable })!;

}