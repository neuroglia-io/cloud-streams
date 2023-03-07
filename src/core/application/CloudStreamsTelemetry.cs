using System.Diagnostics;

namespace CloudStreams.Core.Application;

/// <summary>
/// Exposes constants about Cloud Streams application telemetry
/// </summary>
public static class CloudStreamsTelemetry
{

    /// <summary>
    /// Exposes the Cloud Streams application's <see cref="System.Diagnostics.ActivitySource"/>
    /// </summary>
    public static ActivitySource ActivitySource { get; set; } = null!;

}
