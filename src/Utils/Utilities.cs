using System.Reflection;

namespace StableUI.Utils;

/// <summary>General utilities holder.</summary>
public static class Utilities
{
    /// <summary>Used by linked pages to prevent cache errors when data changes.</summary>
    public static string VaryID = Assembly.GetEntryAssembly()?.GetName().Version.ToString();
}
