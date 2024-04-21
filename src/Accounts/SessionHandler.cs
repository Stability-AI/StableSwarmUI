using StableSwarmUI.Utils;
using StableSwarmUI.Core;
using System.Collections.Concurrent;
using LiteDB;
using StableSwarmUI.Text2Image;
using FreneticUtilities.FreneticToolkit;
using FreneticUtilities.FreneticExtensions;

namespace StableSwarmUI.Accounts;

/// <summary>Core manager for sessions.</summary>
public class SessionHandler
{
    /// <summary>How long the random session ID tokens should be.</summary>
    public int SessionIDLength = 40; // TODO: Configurable

    /// <summary>Map of currently tracked sessions by ID.</summary>
    public ConcurrentDictionary<string, Session> Sessions = new();

    /// <summary>Temporary map of current users. Do not use this directly, use <see cref="GetUser(string)"/>.</summary>
    public ConcurrentDictionary<string, User> Users = new();

    /// <summary>ID to use for the local user when in single-user mode.</summary>
    public static string LocalUserID = "local";

    /// <summary>Internal database.</summary>
    public ILiteDatabase Database;

    /// <summary>Internal database (users).</summary>
    public ILiteCollection<User.DatabaseEntry> UserDatabase;

    /// <summary>Internal database (presets).</summary>
    public ILiteCollection<T2IPreset> T2IPresets;

    /// <summary>Generic user data store.</summary>
    public ILiteCollection<GenericDataStore> GenericData;

    /// <summary>Internal database access locker.</summary>
    public LockObject DBLock = new();

    public User GenericSharedUser;

    /// <summary>Helper for the database to store generic datablob.s</summary>
    public class GenericDataStore
    {
        [BsonId]
        public string ID { get; set; }

        public string Data { get; set; }
    }

    public SessionHandler()
    {
        Database = new LiteDatabase($"{Program.DataDir}/Users.ldb");
        UserDatabase = Database.GetCollection<User.DatabaseEntry>("users");
        T2IPresets = Database.GetCollection<T2IPreset>("t2i_presets");
        GenericData = Database.GetCollection<GenericDataStore>("generic_data");
        GenericSharedUser = GetUser("__shared");
    }

    public Session CreateAdminSession(string source, string userId = null)
    {
        if (HasShutdown)
        {
            throw new InvalidOperationException("Session handler is shutting down.");
        }
        userId ??= LocalUserID;
        User user = GetUser(userId);
        user.Restrictions.Admin = true;
        Logs.Info($"Creating new admin session '{userId}' for {source}");
        for (int i = 0; i < 1000; i++)
        {
            Session sess = new()
            {
                ID = Utilities.SecureRandomHex(SessionIDLength),
                OriginAddress = source,
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

    /// <summary>Gets or creates the user for the given ID.</summary>
    public User GetUser(string userId)
    {
        userId = Utilities.StrictFilenameClean(userId).Replace("/", "");
        if (userId.Length == 0)
        {
            userId = "_";
        }
        return Users.GetOrCreate(userId, () =>
        {
            User.DatabaseEntry userData = UserDatabase.FindById(userId);
            userData ??= new() { ID = userId, RawSettings = "\n" };
            return new(this, userData);
        });
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
