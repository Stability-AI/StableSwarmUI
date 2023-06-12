using FreneticUtilities.FreneticToolkit;
using FreneticUtilities.FreneticDataSyntax;
using LiteDB;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Utils;

namespace StableUI.Accounts;

/// <summary>Represents a single user account.</summary>
public class User
{
    /// <summary>Data for the user that goes directly to the database.</summary>
    public class DatabaseEntry
    {
        [BsonId]
        public string _id;

        /// <summary>What presets this user has saved, matched to the preset database.</summary>
        public List<string> Presets = new();

        /// <summary>This users stored settings data.</summary>
        public string RawSettings = "";
    }

    public User(SessionHandler sessions, DatabaseEntry data)
    {
        Sessions = sessions;
        Data = data;
        Settings.Load(new FDSSection(data.RawSettings));
    }

    /// <summary>Save this user's data to the backend handler.</summary>
    public void Save()
    {
        Sessions.UserDatabase.Upsert(Data);
    }

    /// <summary>The relevant sessions handler backend.</summary>
    public SessionHandler Sessions;

    /// <summary>Core data for this user in the backend database.</summary>
    public DatabaseEntry Data;

    /// <summary>The short static User-ID for this user.</summary>
    public string UserID => Data._id;

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
                "year" => $"{time.Year}",
                "month" => $"{time.Month}",
                "month_name" => $"{time:MMMM}",
                "day" => $"{time.Day}",
                "day_name" => $"{time:dddd}",
                "hour" => $"{time.Hour}",
                "minute" => $"{time.Minute}",
                "second" => $"{time.Second}",
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
