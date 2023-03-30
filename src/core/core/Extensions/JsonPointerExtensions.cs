using CloudStreams.Core;
using Json.Pointer;

namespace CloudStreams;

/// <summary>
/// Defines extensions for <see cref="JsonPointer"/>s
/// </summary>
public static class JsonPointerExtensions
{

    /// <summary>
    /// Converts the <see cref="JsonPointer"/> to a new camel-cased <see cref="JsonPointer"/>
    /// </summary>
    /// <param name="pointer">The <see cref="JsonPointer"/> to convert to camel case</param>
    /// <returns>A new camel-cased <see cref="JsonPointer"/></returns>
    public static JsonPointer ToCamelCase(this JsonPointer pointer) => JsonPointer.Create(pointer.Segments.Select(s => PointerSegment.Create(s.Value.ToCamelCase())).ToArray());

}