using System.Security.Cryptography;
using System.Text;

namespace CloudStreams;

/// <summary>
/// Defines helpers methods for generating hashes
/// </summary>
public static class HashHelper
{

    /// <summary>
    /// Generates a new hash using the specified <see cref="HashAlgorithm"/>
    /// </summary>
    /// <param name="hashAlgorithm">The <see cref="HashAlgorithm"/> to use</param>
    /// <param name="value">The value to hash</param>
    /// <returns>The hashed value</returns>
    public static string Hash(HashAlgorithmName hashAlgorithm, string value)
    {
        if (hashAlgorithm == HashAlgorithmName.SHA256) SHA256Hash(value);
        else if (hashAlgorithm == HashAlgorithmName.MD5) MD5Hash(value);
        throw new NotSupportedException($"The specified {nameof(HashAlgorithm)} '{hashAlgorithm}' is not supported");
    }

    /// <summary>
    /// Generates a new SHA256 hash
    /// </summary>
    /// <param name="value">The value to hash</param>
    /// <returns>The hashed value</returns>
    public static string SHA256Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a new MD5 hash
    /// </summary>
    /// <param name="value">The value to hash</param>
    /// <returns>The hashed value</returns>
    public static string MD5Hash(string value)
    {
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

}