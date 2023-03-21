namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="FileInfo"/>s
/// </summary>
public static class FileInfoExtensions
{

    /// <summary>
    /// Determines whether or not the <see cref="FileInfo"/> is a JSON file
    /// </summary>
    /// <param name="file">The <see cref="FileInfo"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="FileInfo"/> is a JSON file</returns>
    public static bool IsJson(this FileInfo file) => file.Extension.EndsWith(".json");

    /// <summary>
    /// Determines whether or not the <see cref="FileInfo"/> is a YAML file
    /// </summary>
    /// <param name="file">The <see cref="FileInfo"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="FileInfo"/> is a YAML file</returns>
    public static bool IsYaml(this FileInfo file) => file.Extension.EndsWith(".yaml") || file.Extension.EndsWith(".yml");

}
