namespace StableSwarmUI.Core;

/// <summary>Abstract representation of an extension. Extensions should have a 'main' class that derives from this one.</summary>
public abstract class Extension
{
    /// <summary>Automatically calculated path to this extension's directory (relative to process root path), eg "src/Extensions/MyExtension/".</summary>
    public string FilePath;

    /// <summary>Automatically set, extension internal name. Editing this is a bad idea.</summary>
    public string ExtensionName;

    /// <summary>Optional, filenames (relative to extension directory) of additional script files to use, eg "Assets/my_ext.js". You should populate this during <see cref="OnInit"/> or earlier.</summary>
    public List<string> ScriptFiles = [];

    /// <summary>Optional, filenames (relative to extension directory) of additional CSS files to use, eg "Assets/my_ext.css". You should populate this during <see cref="OnInit"/> or earlier.</summary>
    public List<string> StyleSheetFiles = [];

    /// <summary>Called when the extension is initialized for the first time, before settings or anything else is loaded, very early in the extension cycle.</summary>
    public virtual void OnFirstInit()
    {
    }

    /// <summary>Called after settings are loaded, but before the program starts loading.</summary>
    public virtual void OnPreInit()
    {
    }

    /// <summary>Called after settings are loaded and program features are prepped, but before they fully init. This is the ideal place for registering backends, features, etc.</summary>
    public virtual void OnInit()
    {
    }

    /// <summary>Called after the rest of the program has loaded, but just before it has actually launched.</summary>
    public virtual void OnPreLaunch()
    {
    }

    /// <summary>Called when the extension is shutting down (and/or the whole program is). Note that this is not strictly guaranteed to be called (eg if the process crashes).</summary>
    public virtual void OnShutdown()
    {
    }
}
