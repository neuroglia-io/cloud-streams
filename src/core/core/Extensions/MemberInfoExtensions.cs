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

using System.Reflection;

namespace CloudStreams.Core;

/// <summary>
/// Defines extension methods for <see cref="MemberInfo"/>s
/// </summary>
public static class MemberInfoExtensions
{

    /// <summary>
    /// Attempts to get a custom attribute of the specified <see cref="MemberInfo"/>
    /// </summary>
    /// <typeparam name="TAttribute">The type of the custom attribute to get</typeparam>
    /// <param name="extended">The extended <see cref="MemberInfo"/></param>
    /// <param name="attribute">The resulting custom attribute</param>
    /// <returns>A boolean indicating whether or not the custom attribute of the specified <see cref="MemberInfo"/> could be found</returns>
    public static bool TryGetCustomAttribute<TAttribute>(this MemberInfo extended, out TAttribute? attribute)
        where TAttribute : Attribute
    {
        attribute = extended.GetCustomAttribute<TAttribute>();
        return attribute != null;
    }

    /// <summary>
    /// Gets the <see cref="MemberInfo"/> overriden by the <see cref="MemberInfo"/>
    /// </summary>
    /// <param name="extended">The <see cref="MemberInfo"/> to get the overriden <see cref="MemberInfo"/> for</param>
    /// <returns>The <see cref="MemberInfo"/> overriden by the <see cref="MemberInfo"/>, if any</returns>
    public static MemberInfo? GetOverridenMember(this MemberInfo extended)
    {
        if (extended == null) throw new ArgumentNullException(nameof(extended));
        var declaringType = extended.DeclaringType!;
        if (declaringType == extended.ReflectedType) declaringType = declaringType.GetDeclaringTypeOf(extended);
        if (declaringType == null) return null;
        return extended switch
        {
            ConstructorInfo constructor => extended,
            FieldInfo field => declaringType.GetField(field.Name),
            PropertyInfo property => declaringType.GetProperty(property.Name),
            MethodInfo method => declaringType.GetMethod(method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray()),
            _ => throw new NotSupportedException($"The specified member type '{extended.MemberType}' is not supported"),
        };
    }

}