using FreneticUtilities.FreneticToolkit;
using FreneticUtilities.FreneticDataSyntax;
using LiteDB;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Utils;
using StableUI.Text2Image;
using FreneticUtilities.FreneticExtensions;
using System.Xml.Linq;

namespace StableUI.Accounts;

/// <summary>Represents a single user account.</summary>
public class User
{
    /// <summary>Data for the user that goes directly to the database.</summary>
    public class DatabaseEntry
    {
        [BsonId]
        public string ID { get; set; }

        /// <summary>What presets this user has saved, matched to the preset database.</summary>
        public List<string> Presets { get; set; } = new();

        /// <summary>This users stored settings data.</summary>
        public string RawSettings { get; set; } = "";
    }

    public User(SessionHandler sessions, DatabaseEntry data)
    {
        Sessions = sessions;
        Data = data;
        Settings.Load(Program.ServerSettings.DefaultUser.Save(false));
        foreach (string field in Settings.InternalData.SharedData.Fields.Keys)
        {
            Settings.TrySetFieldModified(field, false);
        }
        Settings.Load(new FDSSection(data.RawSettings));
    }

    /// <summary>Save this user's data to the backend handler.</summary>
    public void Save()
    {
        Data.RawSettings = Settings.Save(false).ToString();
        lock (Sessions.DBLock)
        {
            Sessions.UserDatabase.Upsert(Data);
        }
    }

    /// <summary>Returns the user preset for the given name, or null if not found.</summary>
    public T2IPreset GetPreset(string name)
    {
        lock (Sessions.DBLock)
        {
            return Sessions.T2IPresets.FindById($"{UserID}///{name.ToLowerFast()}");
        }
    }

    /// <summary>Returns a list of all presets this user has saved.</summary>
    public List<T2IPreset> GetAllPresets()
    {
        lock (Sessions.DBLock)
        {
            List<T2IPreset> presets = Data.Presets.Select(p => Sessions.T2IPresets.FindById(p)).ToList();
            if (presets.Any(p => p is null))
            {
                List<string> bad = Data.Presets.Where(p => Sessions.T2IPresets.FindById(p) is null).ToList();
                Logs.Error($"User {UserID} has presets that don't exist (database error?): {string.Join(", ", bad)}");
                presets.RemoveAll(p => p is null);
            }
            return presets;
        }
    }

    /// <summary>Saves a new preset on the user's account.</summary>
    public void SavePreset(T2IPreset preset)
    {
        lock (Sessions.DBLock)
        {
            preset.ID = $"{UserID}///{preset.Title.ToLowerFast()}";
            Sessions.T2IPresets.Upsert(preset.ID, preset);
            if (!Data.Presets.Contains(preset.ID))
            {
                Data.Presets.Add(preset.ID);
            }
            Save();
        }
    }

    /// <summary>Deletes a user preset, returns true if anything was deleted.</summary>
    public bool DeletePreset(string name)
    {
        lock (Sessions.DBLock)
        {
            string id = $"{UserID}///{name.ToLowerFast()}";
            if (Data.Presets.Remove(id))
            {
                Sessions.T2IPresets.Delete(id);
                Save();
                return true;
            }
            return false;
        }
    }

    /// <summary>The relevant sessions handler backend.</summary>
    public SessionHandler Sessions;

    /// <summary>Core data for this user in the backend database.</summary>
    public DatabaseEntry Data;

    /// <summary>The short static User-ID for this user.</summary>
    public string UserID => Data.ID;

    /// <summary>What restrictions apply to this user.</summary>
    public Settings.UserRestriction Restrictions = new();

    /// <summary>This user's settings.</summary>
    public Settings.User Settings = new();

    /// <summary>Path to the output directory appropriate to this session.</summary>
    public string OutputDirectory => $"{Program.ServerSettings.OutputPath}/{UserID}";

    public LockObject UserLock = new();

    /// <summary>Returns whether this user has the given generic permission flag.</summary>
    public bool HasGenericPermission(string permName)
    {
        return Restrictions.PermissionFlags.Contains(permName) || Restrictions.PermissionFlags.Contains("*");
    }

    /// <summary>Converts the user's output path setting to a real path for the given parameters. Note that the path is partially cleaned, but not completely.</summary>
    public string BuildImageOutputPath(T2IParams user_input)
    {
        int maxLen = Settings.OutPathBuilder.MaxLenPerPart;
        DateTimeOffset time = DateTimeOffset.Now;
        string buildPathPart(string part)
        {
            string data = part switch
            {
                "year" => $"{time.Year:0000}",
                "month" => $"{time.Month:00}",
                "month_name" => $"{time:MMMM}",
                "day" => $"{time.Day:00}",
                "day_name" => $"{time:dddd}",
                "hour" => $"{time.Hour:00}",
                "minute" => $"{time.Minute:00}",
                "second" => $"{time.Second:00}",
                "prompt" => user_input.Prompt,
                "negative_prompt" => user_input.NegativePrompt,
                "seed" => $"{user_input.Seed}",
                "cfg_scale" => $"{user_input.CFGScale}",
                "width" => $"{user_input.Width}",
                "height" => $"{user_input.Height}",
                "steps" => $"{user_input.Steps}",
                "var_seed" => $"{user_input.VarSeed}",
                "var_strength" => $"{user_input.VarSeedStrength}",
                "model" => user_input.Model?.Name ?? "unknown",
                "user_name" => UserID,
                string other => user_input.OtherParams.TryGetValue(other, out object val) ? val.ToString() : null
            };
            if (data is null)
            {
                return null;
            }
            if (data.Length > maxLen)
            {
                data = data[..maxLen];
            }
            data = data.Replace("/", "");
            return data;
        }
        string path = Settings.OutPathBuilder.Format;
        path = StringConversionHelper.QuickSimpleTagFiller(path, "[", "]", buildPathPart);
        path = Utilities.FilePathForbidden.TrimToNonMatches(path).Replace(".", "");
        if (path.Length < 5) // Quiet trick: some short file names, eg 'CON.png', would hit Windows reserved names, so quietly break that.
        {
            path = $"{path}_";
        }
        return path;
    }
}
