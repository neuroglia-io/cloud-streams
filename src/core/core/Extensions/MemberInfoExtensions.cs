using System.Reflection;

namespace CloudStreams;

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

}