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

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CloudStreams.Core;

/// <summary>
/// Defines helpers to manage the tokens subscribers present to acknowledge cloud events whose acknowledgement they have deferred
/// </summary>
/// <remarks>
/// The token is the only authorization required to acknowledge a cloud event. Only its hash is therefore stored in the subscription's status, so that anyone allowed
/// to read subscriptions cannot acknowledge on behalf of their subscribers (see #149)
/// </remarks>
public static class AcknowledgementTokens
{

    /// <summary>
    /// Generates a new, random acknowledgement token
    /// </summary>
    /// <returns>A new acknowledgement token</returns>
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Computes the hash of the specified acknowledgement token, which is what the subscription's status stores
    /// </summary>
    /// <param name="token">The acknowledgement token to hash</param>
    /// <returns>The hash of the specified acknowledgement token</returns>
    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>
    /// Determines, in fixed time, whether the specified token matches the specified hash
    /// </summary>
    /// <param name="token">The token presented by the subscriber</param>
    /// <param name="hash">The hash stored in the subscription's status</param>
    /// <returns>A boolean indicating whether or not the token matches the hash</returns>
    public static bool Matches(string token, string hash)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(hash)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(token)), Encoding.UTF8.GetBytes(hash));
    }

}
