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

    /// <summary>Converts the user's output path setting to a real path for the given parameters. Note that the path is partially cleaned, but not completely.</summary>
    public string BuildImageOutputPath(T2IParams user_input)
    {
        string path = Settings.OutPathBuilder.Format;
        int maxLen = Settings.OutPathBuilder.MaxLenPerPart;
        foreach (DataHolderHelper.FieldData field in DataHolderHelper<T2IParams>.Instance.Fields)
        {
            string piece = field.Field.GetValue(user_input).ToString();
            if (piece.Length > maxLen)
            {
                piece = piece[..maxLen];
            }
            path = path.Replace($"[{field.Name}]", piece.Replace("//", ""));
        }
        path = Utilities.FilePathForbidden.TrimToNonMatches(path).Replace(".", "");
        if (path.Length < 5) // Quiet trick: some short file names, eg 'CON.png', would hit Windows reserved names, so quietly break that.
        {
            path = $"{path}_";
        }
        return path;
    }
}
