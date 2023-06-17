using Microsoft.AspNetCore.StaticFiles;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Utils;
using System.Collections.Concurrent;

namespace StableUI.Accounts;

/// <summary>Container for information related to an active session.</summary>
public class Session
{
    /// <summary>Randomly generated ID.</summary>
    public string ID;

    /// <summary>The relevant <see cref="User"/>.</summary>
    public User User;

    public CancellationTokenSource SessInterrupt = new();

    public ConcurrentDictionary<GenClaim, GenClaim> Claims = new();

    /// <summary>The number of generations currently running on this session.</summary>
    public volatile int WaitingGenerations = 0;

    /// <summary>Use "using <see cref="GenClaim"/> claim = session.Claim(image_count);" to track generation requests pending on this session.</summary>
    public GenClaim Claim(int amt)
    {
        return new(this, amt);
    }

    /// <summary>Helper to claim an amount of generations and dispose it automatically cleanly.</summary>
    public class GenClaim : IDisposable
    {
        /// <summary>The number of generations tracked by this object.</summary>
        public volatile int Amount;

        /// <summary>The relevant original session.</summary>
        public Session Sess;

        /// <summary>Cancel token that cancels if the user wants to interrupt all generations.</summary>
        public CancellationToken InterruptToken;

        /// <summary>If true, the running generations should stop immediately.</summary>
        public bool ShouldCancel => InterruptToken.IsCancellationRequested;

        public GenClaim(Session session, int amt)
        {
            Sess = session;
            Amount = amt;
            InterruptToken = session.SessInterrupt.Token;
            Interlocked.Add(ref session.WaitingGenerations, amt);
            session.Claims.TryAdd(this, this);
        }

        /// <summary>Increase the size of the claim.</summary>
        public void Extend(int count)
        {
            Interlocked.Add(ref Amount, count);
            Interlocked.Add(ref Sess.WaitingGenerations, count);
        }

        /// <summary>Mark a subset of these as complete.</summary>
        public void Complete(int count)
        {
            Interlocked.Add(ref Amount, -count);
            Interlocked.Add(ref Sess.WaitingGenerations, -count);
        }

        /// <summary>Internal dispose route, called by 'using' statements.</summary>
        public void Dispose()
        {
            Interlocked.Add(ref Sess.WaitingGenerations, -Amount);
            Sess.Claims.TryRemove(this, out _);
            Amount = 0;
            GC.SuppressFinalize(this);
        }

        ~GenClaim()
        {
            Dispose();
        }
    }

    /// <summary>Save an image as this user, and returns the new URL. If user has disabled saving, returns a data URL.</summary>
    public string SaveImage(Image image, T2IParams user_input)
    {
        // TODO: File conversion, metadata, etc.
        if (!User.Settings.SaveFiles)
        {
            return "data:image/png;base64," + image.AsBase64;
        }
        string rawImagePath = User.BuildImageOutputPath(user_input);
        string imagePath = rawImagePath;
        string extension = "png";
        string fullPath = $"{User.OutputDirectory}/{imagePath}.{extension}";
        lock (User.UserLock)
        {
            try
            {
                int num = 0;
                while (File.Exists(fullPath))
                {
                    num++;
                    imagePath = $"{rawImagePath}-{num}";
                    fullPath = $"{User.OutputDirectory}/{imagePath}.{extension}";
                }
                Directory.CreateDirectory(Directory.GetParent(fullPath).FullName);
                File.WriteAllBytes(fullPath, image.ImageData);
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
                    return "ERROR";
                }
            }
        }
        return $"Output/{imagePath}.{extension}";
    }
}
