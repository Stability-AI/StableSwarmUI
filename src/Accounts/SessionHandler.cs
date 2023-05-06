using StableUI.Utils;
using System.Collections.Concurrent;

namespace StableUI.Accounts;

/// <summary>Core manager for sessions.</summary>
public class SessionHandler
{
    /// <summary>How long the random session ID tokens should be.</summary>
    public static int SessionIDLength = 40; // TODO: Configurable

    /// <summary>Map of currently tracked sessions by ID.</summary>
    public static ConcurrentDictionary<string, Session> Sessions = new();

    public static Session CreateAdminSession(string source)
    {
        Logs.Info($"Creating new admin session for {source}");
        return new()
        {
            OutputDirectory = "outputs/main/",
            ID = Utilities.SecureRandomHex(SessionIDLength)
        };
    }
}
