// Copyright © 2024-Present The Cloud Streams Authors
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

namespace CloudStreams.Core;

/// <summary>
/// Matches cloud event sources against the patterns publishers are bound to
/// </summary>
/// <remarks>
/// A pattern is matched against the whole source, character for character, except for <c>*</c>, which matches any sequence of characters, including none: for example,
/// <c>https://orders.example.com/*</c> matches every source under <c>https://orders.example.com/</c>, but not <c>https://orders.example.com.evil.net</c>.
/// Wildcards rather than regular expressions, so that a pattern cannot accidentally match more than it reads (see #147)
/// </remarks>
public static class CloudEventSourcePattern
{

    /// <summary>
    /// Gets the character that matches any sequence of characters
    /// </summary>
    public const char Wildcard = '*';

    /// <summary>
    /// Determines whether the specified source matches any of the specified patterns
    /// </summary>
    /// <param name="source">The source to match</param>
    /// <param name="patterns">The patterns to match the source against</param>
    /// <returns>A boolean indicating whether or not the source matches at least one of the patterns</returns>
    public static bool IsMatchAny(Uri source, IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(patterns);
        return patterns.Any(p => IsMatch(source.OriginalString, p));
    }

    /// <summary>
    /// Determines whether the specified input matches the specified pattern
    /// </summary>
    /// <param name="input">The input to match</param>
    /// <param name="pattern">The pattern to match the input against</param>
    /// <returns>A boolean indicating whether or not the input matches the pattern</returns>
    public static bool IsMatch(string input, string pattern)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        // greedy matching with backtracking to the last wildcard only, which runs in O(input * pattern) at worst
        int i = 0, p = 0, lastWildcard = -1, resumeAt = 0;
        while (i < input.Length)
        {
            if (p < pattern.Length && pattern[p] == Wildcard)
            {
                lastWildcard = p++;
                resumeAt = i;
            }
            else if (p < pattern.Length && pattern[p] == input[i])
            {
                p++;
                i++;
            }
            else if (lastWildcard >= 0)
            {
                p = lastWildcard + 1;
                i = ++resumeAt;
            }
            else return false;
        }
        while (p < pattern.Length && pattern[p] == Wildcard) p++;
        return p == pattern.Length;
    }

}
