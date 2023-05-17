namespace StableUI.Core;

/// <summary>Abstract representation of an extension. Extensions should have a 'main' class that derives from this one.</summary>
public abstract class Extension
{
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
}
