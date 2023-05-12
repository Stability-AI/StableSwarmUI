using Newtonsoft.Json.Linq;

namespace StableUI.Text2Image;

/// <summary>Represents the basic data around an (unloaded) Text2Image model.</summary>
public class T2IModel
{
    /// <summary>Name/ID of the model.</summary>
    public string Name;

    /// <summary>Full raw system filepath to this model.</summary>
    public string RawFilePath;

    /// <summary>Description text, if any, of the model.</summary>
    public string Description;

    /// <summary>What type of model this is, if known.</summary>
    public string Type;

    /// <summary>URL or data blob of a preview image for this model.</summary>
    public string PreviewImage;

    /// <summary>If true, at least one backend has this model currently loaded.</summary>
    public bool AnyBackendsHaveLoaded = false;

    /// <summary>Gets a networkable copy of this model's data.</summary>
    public JObject ToNetObject()
    {
        return new JObject()
        {
            ["name"] = Name,
            ["description"] = Description,
            ["type"] = Type,
            ["preview_image"] = PreviewImage,
            ["loaded"] = AnyBackendsHaveLoaded
        };
    }
}
