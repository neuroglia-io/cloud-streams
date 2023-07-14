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

using CloudStreams.Core.Data;
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
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (e == null) throw new ArgumentNullException(nameof(e));
        return (configuration.Source.Trim() == "*" || Regex.IsMatch(e.Source.OriginalString, configuration.Source))
            && (configuration.Type.Trim() == "*" || Regex.IsMatch(e.Type, configuration.Type));
    }

}
