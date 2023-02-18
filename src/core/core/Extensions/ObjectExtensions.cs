namespace CloudStreams;

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

}