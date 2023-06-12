using FreneticUtilities.FreneticToolkit;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Utils;

namespace StableUI.Accounts;

/// <summary>Represents a single user account.</summary>
public class User
{
    /// <summary>The short static User-ID for this user.</summary>
    public string UserID;

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
