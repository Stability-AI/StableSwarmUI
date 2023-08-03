using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.IO;

namespace StableSwarmUI.Accounts;

/// <summary>Container for information related to an active session.</summary>
public class Session : IEquatable<Session>
{
    /// <summary>Randomly generated ID.</summary>
    public string ID;

    /// <summary>The relevant <see cref="User"/>.</summary>
    public User User;

    public CancellationTokenSource SessInterrupt = new();

    public List<GenClaim> Claims = new();

    /// <summary>Statistics about the generations currently waiting in this session.</summary>
    public int WaitingGenerations = 0, LoadingModels = 0, WaitingBackends = 0, LiveGens = 0;

    /// <summary>Locker for interacting with this session's statsdata.</summary>
    public LockObject StatsLocker = new();

    /// <summary>Use "using <see cref="GenClaim"/> claim = session.Claim(image_count);" to track generation requests pending on this session.</summary>
    public GenClaim Claim(int gens = 0, int modelLoads = 0, int backendWaits = 0, int liveGens = 0)
    {
        return new(this, gens, modelLoads, backendWaits, liveGens);
    }

    /// <summary>Helper to claim an amount of generations and dispose it automatically cleanly.</summary>
    public class GenClaim : IDisposable
    {
        /// <summary>The number of generations tracked by this object.</summary>
        public int WaitingGenerations = 0, LoadingModels = 0, WaitingBackends = 0, LiveGens = 0;

        /// <summary>The relevant original session.</summary>
        public Session Sess;

        /// <summary>Cancel token that cancels if the user wants to interrupt all generations.</summary>
        public CancellationToken InterruptToken;

        /// <summary>Token source to interrupt just this claim's set.</summary>
        public CancellationTokenSource LocalClaimInterrupt = new();

        /// <summary>If true, the running generations should stop immediately.</summary>
        public bool ShouldCancel => InterruptToken.IsCancellationRequested || LocalClaimInterrupt.IsCancellationRequested;

        public GenClaim(Session session, int gens, int modelLoads, int backendWaits, int liveGens)
        {
            Sess = session;
            InterruptToken = session.SessInterrupt.Token;
            lock (Sess.StatsLocker)
            {
                Extend(gens, modelLoads, backendWaits, liveGens);
                session.Claims.Add(this);
            }
        }

        /// <summary>Increase the size of the claim.</summary>
        public void Extend(int gens = 0, int modelLoads = 0, int backendWaits = 0, int liveGens = 0)
        {
            lock (Sess.StatsLocker)
            {
                WaitingGenerations += gens;
                LoadingModels += modelLoads;
                WaitingBackends += backendWaits;
                LiveGens += liveGens;
                Sess.WaitingGenerations += gens;
                Sess.LoadingModels += modelLoads;
                Sess.WaitingBackends += backendWaits;
                Sess.LiveGens += liveGens;
            }
        }

        /// <summary>Mark a subset of these as complete.</summary>
        public void Complete(int gens = 0, int modelLoads = 0, int backendWaits = 0, int liveGens = 0)
        {
            Extend(-gens, -modelLoads, -backendWaits, -liveGens);
        }

        /// <summary>Internal dispose route, called by 'using' statements.</summary>
        public void Dispose()
        {
            lock (Sess.StatsLocker)
            {
                Complete(WaitingGenerations, LoadingModels, WaitingBackends, LiveGens);
                Sess.Claims.Remove(this);
            }
            GC.SuppressFinalize(this);
        }

        ~GenClaim()
        {
            Dispose();
        }
    }

    /// <summary>Applies metadata to an image and converts the filetype, following the user's preferences.</summary>
    public (Image, string) ApplyMetadata(Image image, T2IParamInput user_input, Dictionary<string, object> extraParams)
    {
        string metadata = user_input.GenRawMetadata(extraParams);
        image = image.ConvertTo(User.Settings.FileFormat.ImageFormat, User.Settings.FileFormat.SaveMetadata ? metadata : null, User.Settings.FileFormat.DPI);
        return (image, metadata ?? "");
    }

    /// <summary>Save an image as this user, and returns the new URL. If user has disabled saving, returns a data URL.</summary>
    /// <returns>(User-Visible-WebPath, Local-FilePath)</returns>
    public (string, string) SaveImage(Image image, int batchIndex, T2IParamInput user_input, string metadata)
    {
        if (!User.Settings.SaveFiles)
        {
            return ("data:image/" + (User.Settings.FileFormat.ImageFormat == "png" ? "png" : "jpeg") + ";base64," + image.AsBase64, null);
        }
        string rawImagePath = User.BuildImageOutputPath(user_input, batchIndex);
        string imagePath = rawImagePath.Replace("[number]", "1");
        string extension = (User.Settings.FileFormat.ImageFormat == "png" ? "png" : "jpg");
        string fullPath = $"{User.OutputDirectory}/{imagePath}.{extension}";
        lock (User.UserLock)
        {
            try
            {
                int num = 0;
                while (File.Exists(fullPath))
                {
                    num++;
                    imagePath = rawImagePath.Contains("[number]") ? rawImagePath.Replace("[number]", $"{num}") : $"{rawImagePath}-{num}";
                    fullPath = $"{User.OutputDirectory}/{imagePath}.{extension}";
                }
                Directory.CreateDirectory(Directory.GetParent(fullPath).FullName);
                File.WriteAllBytes(fullPath, image.ImageData);
                if (User.Settings.FileFormat.SaveTextFileMetadata && !string.IsNullOrWhiteSpace(metadata))
                {
                    File.WriteAllBytes(fullPath.BeforeLast('.') + ".txt", metadata.EncodeUTF8());
                }
            }
            catch (Exception)
            {
                try
                {
                    imagePath = "image_name_error/" + Utilities.SecureRandomHex(10);
                    fullPath = $"{User.OutputDirectory}/{imagePath}.{extension}";
                    Directory.CreateDirectory(Directory.GetParent(fullPath).FullName);
                    File.WriteAllBytes(fullPath, image.ImageData);
                }
                catch (Exception ex)
                {
                    Logs.Error($"Could not save user image: {ex.Message}");
                    return ("ERROR", null);
                }
            }
        }
        return ($"Output/{imagePath}.{extension}", fullPath);
    }

    /// <summary>Gets a hash code for this session, for C# equality comparsion.</summary>
    public override int GetHashCode()
    {
        return ID.GetHashCode();
    }

    /// <summary>Returns true if this session is the same as another.</summary>
    public override bool Equals(object obj)
    {
        return obj is Session session && Equals(session);
    }

    /// <summary>Returns true if this session is the same as another.</summary>
    public bool Equals(Session other)
    {
        return ID == other.ID;
    }
}
