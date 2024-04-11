using CloudStreams.Core.Resources;
using System.Text.RegularExpressions;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="CloudEventIngestionConfiguration"/>s
/// </summary>
public static class CloudEventIngestionConfigurationExtensions
{

    /// <summary>
    /// Determines whether or not the <see cref="CloudEventIngestionConfiguration"/> applies to the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="configuration">The <see cref="CloudEventIngestionConfiguration"/> to check</param>
    /// <param name="e">The <see cref="CloudEvent"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="CloudEventIngestionConfiguration"/> applies to the specified <see cref="CloudEvent"/></returns>
    public static bool AppliesTo(this CloudEventIngestionConfiguration configuration, CloudEvent e)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(e);

        return (configuration.Source.Trim() == "*" || Regex.IsMatch(e.Source.OriginalString, configuration.Source))
            && (configuration.Type.Trim() == "*" || Regex.IsMatch(e.Type, configuration.Type));
    }

}