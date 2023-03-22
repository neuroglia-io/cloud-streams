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

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="StreamReadDirection"/>s
/// </summary>
public static class StreamReadDirectionExtensions
{

    /// <summary>
    /// Converts the <see cref="StreamReadDirection"/> into a <see cref="Direction"/>
    /// </summary>
    /// <param name="readDirection">The <see cref="StreamReadDirection"/> to convert</param>
    /// <returns>The converted <see cref="Direction"/></returns>
    public static Direction ToDirection(this StreamReadDirection readDirection)
    {
        return readDirection switch
        {
            StreamReadDirection.Forwards => Direction.Forwards,
            StreamReadDirection.Backwards => Direction.Backwards,
            _ => throw new NotSupportedException($"The specified stream read direction '{readDirection}' is not supported")
        };
    }

}
