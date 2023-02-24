using System.Diagnostics;

namespace CloudStreams.Core.Application;

/// <summary>
/// Exposes constants about Cloud Streams application tracing
/// </summary>
internal static class Tracing
{

    /// <summary>
    /// Exposes the Cloud Streams application's <see cref="System.Diagnostics.ActivitySource"/>
    /// </summary>
    public static ActivitySource ActivitySource { get; set; } = null!;

}
