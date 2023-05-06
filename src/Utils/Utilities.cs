using System.Reflection;
using System.Security.Cryptography;

namespace StableUI.Utils;

/// <summary>General utilities holder.</summary>
public static class Utilities
{
    /// <summary>StableUI's current version.</summary>
    public static readonly string Version = Assembly.GetEntryAssembly()?.GetName().Version.ToString();

    /// <summary>Used by linked pages to prevent cache errors when data changes.</summary>
    public static string VaryID = Version;

    /// <summary>Gets a secure hex string of a given length (will generate half as many bytes).</summary>
    public static string SecureRandomHex(int length)
    {
        if (length % 2 == 1)
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes((length + 1) / 2))[0..^1];
        }
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(length / 2));
    }
}
