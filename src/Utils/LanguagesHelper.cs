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
    public static Dictionary<string, Language> Languages = [];

    /// <summary>Languages expected to be most common, ie should be at top.</summary>
    public static string[] PreferredLanguages = ["en", "zh", "ja"];

    /// <summary>Sorted list of codes. Relevant languages bumped to top, rest alphabetical by English name.</summary>
    public static string[] SortedList;

    /// <summary>Debug/adding set, for developer usage.</summary>
    public static JObject DebugSet = [];

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
        SortedList = [.. Languages.Keys.OrderBy(k => k)];
        if (File.Exists($"./languages/en.debug"))
        {
            DebugSet = (JObject)JObject.Parse(File.ReadAllText($"./languages/en.debug"))["keys"];
        }
    }

    /// <summary>Track a set of translatables in the debug set.</summary>
    public static void AppendSetInternal(params string[] set)
    {
        if (set is null || set.Length == 0)
        {
            return;
        }
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
    }

    /// <summary>Fix formatting on all language files to prevent trouble.</summary>
    public static void FixUpLangs()
    {
        string[] order = ["name_en", "name_local", "authorship", "keys"];
        foreach (string filename in Directory.EnumerateFiles("./languages/", "*.json"))
        {
            JObject data = JObject.Parse(File.ReadAllText(filename));
            data = data.SortByKey(k => Array.IndexOf(order, k));
            data["keys"] = ((JObject)data["keys"]).SortByKey(k => k);
            File.WriteAllText(filename, data.SerializeClean());
        }
    }

    /// <summary>Removes invalid/outdated entries from all language files.</summary>
    public static void RemoveInvalid()
    {
        foreach (string filename in Directory.EnumerateFiles("./languages/", "*.json"))
        {
            JObject data = JObject.Parse(File.ReadAllText(filename));
            data["keys"] = JObject.FromObject((data["keys"] as JObject).Properties().Where(p => DebugSet.ContainsKey(p.Name)).ToDictionary(p => p.Name, p => p.Value));
            File.WriteAllText(filename, data.SerializeClean());
        }
    }

    /// <summary>Track a set of translatables in the debug set.</summary>
    public static void TrackSet(string[] set)
    {
        AppendSetInternal(set);
        DebugSet = DebugSet.SortByKey(k => k);
        File.WriteAllText($"./languages/en.debug", new JObject() { ["keys"] = DebugSet }.SerializeClean());
        RemoveInvalid();
        FixUpLangs();
    }
}
