using StableUI.Utils;
using StableUI.Core;
using System.Collections.Concurrent;

namespace StableUI.Accounts;

/// <summary>Core manager for sessions.</summary>
public class SessionHandler
{
    /// <summary>How long the random session ID tokens should be.</summary>
    public int SessionIDLength = 40; // TODO: Configurable

    /// <summary>Map of currently tracked sessions by ID.</summary>
    public ConcurrentDictionary<string, Session> Sessions = new();

    /// <summary>Basic reusable admin user.</summary>
    public User AdminUser = new() { UserID = "local" };

    public SessionHandler()
    {
        AdminUser.Restrictions.Admin = true;
    }

    public Session CreateAdminSession(string source)
    {
        if (HasShutdown)
        {
            throw new InvalidOperationException("Session handler is shutting down.");
        }
        Logs.Info($"Creating new admin session for {source}");
        for (int i = 0; i < 1000; i++)
        {
            Session sess = new()
            {
                ID = Utilities.SecureRandomHex(SessionIDLength),
                User = AdminUser
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

    private volatile bool HasShutdown;

    /// <summary>Main shutdown handler, triggered by <see cref="Program.Shutdown"/>.</summary>
    public void Shutdown()
    {
        if (HasShutdown)
        {
            return;
        }
        HasShutdown = true;
        Sessions.Clear();
        // TODO
    }
}
