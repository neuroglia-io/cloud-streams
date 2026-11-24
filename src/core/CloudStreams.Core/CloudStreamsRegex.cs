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

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CloudStreams.Core;

/// <summary>
/// Evaluates the regular expressions configured in Cloud Streams resources, such as authorization rules, ingestion configurations and subscription filters
/// </summary>
/// <remarks>
/// <para>
/// Patterns are compiled once and cached, and every match is bounded by <see cref="MatchTimeout"/>, so that a pathological pattern cannot block a thread for each
/// event (see #146). A match that times out is reported as a mismatch.
/// </para>
/// <para>
/// Patterns are not anchored: they match if they match any part of the input. Use <c>^</c> and <c>$</c> to match the whole input, for example <c>^order\.created$</c>
/// </para>
/// </remarks>
public static class CloudStreamsRegex
{

    /// <summary>
    /// Gets the maximum amount of time a single match may take
    /// </summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gets the maximum number of compiled patterns to keep in cache
    /// </summary>
    const int MaxCachedPatterns = 1024;

    static readonly ConcurrentDictionary<string, Regex> Cache = new();

    /// <summary>
    /// Gets the compiled <see cref="Regex"/> for the specified pattern
    /// </summary>
    /// <param name="pattern">The pattern to get the <see cref="Regex"/> for</param>
    /// <returns>The compiled <see cref="Regex"/> for the specified pattern</returns>
    /// <exception cref="ArgumentException">The specified pattern is not a valid regular expression</exception>
    public static Regex Get(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (Cache.TryGetValue(pattern, out var regex)) return regex;
        regex = new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
        // a crude bound rather than an LRU: patterns come from resources, so there are few of them, and the cache only grows if resources keep changing
        if (Cache.Count >= MaxCachedPatterns) Cache.Clear();
        return Cache.GetOrAdd(pattern, regex);
    }

    /// <summary>
    /// Determines whether the specified pattern matches the specified input
    /// </summary>
    /// <param name="input">The input to match</param>
    /// <param name="pattern">The pattern to match the input against</param>
    /// <returns>A boolean indicating whether or not the pattern matches the input. False if the match timed out</returns>
    /// <exception cref="ArgumentException">The specified pattern is not a valid regular expression</exception>
    public static bool IsMatch(string input, string pattern)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            return Get(pattern).IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

}
