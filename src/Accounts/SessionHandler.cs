using StableUI.Utils;
using System.Collections.Concurrent;

namespace StableUI.Accounts;

/// <summary>Core manager for sessions.</summary>
public class SessionHandler
{
    /// <summary>How long the random session ID tokens should be.</summary>
    public int SessionIDLength = 40; // TODO: Configurable

    /// <summary>Map of currently tracked sessions by ID.</summary>
    public ConcurrentDictionary<string, Session> Sessions = new();

    public Session CreateAdminSession(string source)
    {
        Logs.Info($"Creating new admin session for {source}");
        for (int i = 0; i < 1000; i++)
        {
            Session sess = new()
            {
                OutputDirectory = "outputs/main/",
                ID = Utilities.SecureRandomHex(SessionIDLength)
            };
            if (Sessions.TryAdd(sess.ID, sess))
            {
                return sess;
            }
        }
        throw new InvalidOperationException("Something is critically wrong in the session handler, cannot generate unique IDs!");
    }

    /// <summary>Tries to get the session for an id.</summary>
    /// <returns><see cref="true"/> if found, otherwise <see cref="false"/>.</returns>
    public bool TryGetSession(string id, out Session session)
    {
        return Sessions.TryGetValue(id, out session);
    }
        {
            OutputDirectory = "outputs/main/",
            ID = Utilities.SecureRandomHex(SessionIDLength)
        };
    }
}
