using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using System.IO;

namespace StableSwarmUI.Utils;

/// <summary>Small helper class to track what languages are available.</summary>
public class LanguagesHelper
{
    // TODO: This technically preloads all languages on startup, which wastes time and RAM... but not much.
    // If it ever ends up being much, need to reorganize.

    /// <summary>Represents a translatable language.</summary>
    /// <param name="Code">The language code.</param>
    /// <param name="Name">The name of the language in English.</param>
    /// <param name="LocalName">The name of the language in its own language.</param>
    /// <param name="Keys">The underlying raw key datablob.</param>
    public record class Language(string Code, string Name, string LocalName, JObject Keys)
    {
        public JObject ToJSON() => new()
        {
            ["code"] = Code,
            ["name"] = Name,
            ["local_name"] = LocalName,
            ["keys"] = Keys
        };
    }

    /// <summary>Map of all known languages by code.</summary>
    public static Dictionary<string, Language> Languages = new();

    /// <summary>Languages expected to be most common, ie should be at top.</summary>
    public static string[] PreferredLanguages = new[] { "en", "zh", "ja" };

    /// <summary>Sorted list of codes. Relevant languages bumped to top, rest alphabetical by English name.</summary>
    public static string[] SortedList;

    /// <summary>Debug/adding set, for developer usage.</summary>
    public static JObject DebugSet = new();

    /// <summary>Load all languages.</summary>
    public static void LoadAll()
    {
        foreach (string file in Directory.EnumerateFiles("./languages/", "*.json"))
        {
            JObject data = JObject.Parse(File.ReadAllText(file));
            string code = file.Replace('\\', '/').AfterLast('/').BeforeLast('.');
            if (!data.TryGetValue("name_en", out JToken nameEn) || !data.TryGetValue("name_local", out JToken localName) || !data.TryGetValue("keys", out JToken keys))
            {
                Logs.Error($"[Languages] Language file '{file}' is missing required keys! Check documentation. Found keys: [{data.Properties().Select(p => p.Name).JoinString(", ")}], require [name_en, name_local, keys]");
                continue;
            }
            Languages.Add(code, new(code, nameEn.ToString(), localName.ToString(), (JObject)keys));
        }
        SortedList = Languages.Keys.OrderBy(k => k).ToArray();
        if (File.Exists($"./languages/en.debug"))
        {
            DebugSet = (JObject)JObject.Parse(File.ReadAllText($"./languages/en.debug"))["keys"];
        }
    }

    /// <summary>Track a set of translatables in the debug set.</summary>
    public static void TrackSet(string[] set)
    {
        foreach (string s in set)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                continue;
            }
            if (double.TryParse(s.Trim(), out _))
            {
                continue;
            }
            DebugSet[s] = "";
        }
        File.WriteAllText($"./languages/en.debug", new JObject() { ["keys"] = DebugSet }.ToString(Newtonsoft.Json.Formatting.Indented));
    }
}
