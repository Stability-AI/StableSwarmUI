using StableSwarmUI.Utils;
using StableSwarmUI.Core;
using System.Collections.Concurrent;
using LiteDB;
using StableSwarmUI.Text2Image;
using FreneticUtilities.FreneticToolkit;

namespace StableSwarmUI.Accounts;

/// <summary>Core manager for sessions.</summary>
public class SessionHandler
{
    /// <summary>How long the random session ID tokens should be.</summary>
    public int SessionIDLength = 40; // TODO: Configurable

    /// <summary>Map of currently tracked sessions by ID.</summary>
    public ConcurrentDictionary<string, Session> Sessions = new();

    /// <summary>ID to use for the local user when in single-user mode.</summary>
    public static string LocalUserID = "local";

    /// <summary>Internal database.</summary>
    public ILiteDatabase Database;

    /// <summary>Internal database (users).</summary>
    public ILiteCollection<User.DatabaseEntry> UserDatabase;

    /// <summary>Internal database (presets).</summary>
    public ILiteCollection<T2IPreset> T2IPresets;

    /// <summary>Internal database access locker.</summary>
    public LockObject DBLock = new();

    public SessionHandler()
    {
        Database = new LiteDatabase("Data/Users.ldb");
        UserDatabase = Database.GetCollection<User.DatabaseEntry>("users");
        T2IPresets = Database.GetCollection<T2IPreset>("t2i_presets");
    }

    public Session CreateAdminSession(string source, string userId = null)
    {
        if (HasShutdown)
        {
            throw new InvalidOperationException("Session handler is shutting down.");
        }
        userId ??= LocalUserID;
        userId = Utilities.StrictFilenameClean(userId).Replace("/", "");
        if (userId.Length == 0)
        {
            userId = "_";
        }
        User.DatabaseEntry adminUserData = UserDatabase.FindById(userId);
        adminUserData ??= new() { ID = userId, RawSettings = "\n" };
        User user = new(this, adminUserData);
        user.Restrictions.Admin = true;
        Logs.Info($"Creating new admin session for {source}");
        for (int i = 0; i < 1000; i++)
        {
            Session sess = new()
            {
                ID = Utilities.SecureRandomHex(SessionIDLength),
                User = user
            };
            if (Sessions.TryAdd(sess.ID, sess))
            {
                sess.User.CurrentSessions[sess.ID] = sess;
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
        Logs.Info("Will shut down session handler...");
        lock (DBLock)
        {
            Sessions.Clear();
            Logs.Info("Will save user data.");
            Database.Dispose();
        }
        Logs.Info("Session handler is shut down.");
    }
}
