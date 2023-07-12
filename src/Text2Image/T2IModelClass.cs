using Newtonsoft.Json.Linq;

namespace StableSwarmUI.Text2Image;

/// <summary>Represents a class of models (eg SDv1).</summary>
public class T2IModelClass
{
    /// <summary>Standard resolution for this model class.</summary>
    public int StandardWidth, StandardHeight;

    /// <summary>A clean name for this model class.</summary>
    public string Name;

    /// <summary>Matcher, return true if the model x safetensors header is the given class, or false if not.</summary>
    public Func<T2IModel, JObject, bool> IsThisModelOfClass;
}
