using FreneticUtilities.FreneticExtensions;
using StableSwarmUI.Core;
using System.IO;

namespace StableSwarmUI.Utils;

/// <summary>Central class for processing wildcard files.</summary>
public class WildcardsHelper
{
    /// <summary>Internal tracker of all currently known wildcard files. Values populate on first read.</summary>
    public static ConcurrentDictionary<string, string[]> WildcardFiles = new();

    public static string Folder => Utilities.CombinePathWithAbsolute(Program.ServerSettings.Paths.DataPath, Program.ServerSettings.Paths.WildcardsFolder);

    /// <summary>Returns a list of all known wildcard files.</summary>
    public static string[] ListFiles => WildcardFiles.Keys.ToArray();

    /// <summary>Initializes the engine.</summary>
    public static void Init()
    {
        Refresh();
        Program.ModelRefreshEvent += Refresh;
    }

    /// <summary>Refreshes the wildcard tracker, reloading from folder.</summary>
    public static void Refresh()
    {
        WildcardFiles.Clear();
        Directory.CreateDirectory(Folder);
        foreach (string str in Directory.EnumerateFiles(Folder, "*.txt", SearchOption.AllDirectories))
        {
            string path = Path.GetRelativePath(Folder, str).Replace("\\", "/").TrimStart('/').BeforeLast('.');
            WildcardFiles.TryAdd(path, null);
        }
    }

    /// <summary>Gets all possible options for the specified exact wildcard name.</summary>
    public static string[] GetOptions(string card)
    {
        if (!WildcardFiles.TryGetValue(card, out string[] options))
        {
            return null;
        }
        if (options is null)
        {
            options = File.ReadAllText($"{Folder}/{card}.txt").Replace("\r\n", "\n").Replace("\r", "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            WildcardFiles[card] = options;
        }
        return options;
    }

    /// <summary>Picks a random entry from the given wildcard name, for the given random provider.</summary>
    /// <param name="card">The exact wildcard name.</param>
    /// <param name="random">The random provider.</param>
    /// <returns>The random value, or null if invalid.</returns>
    public static string PickRandom(string card, Random random)
    {
        string[] options = GetOptions(card);
        if (options is null)
        {
            return null;
        }
        return options[random.Next(options.Length)];
    }
}
