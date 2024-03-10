using LiteDB;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Utils;

namespace StableSwarmUI.Text2Image;

/// <summary>User-saved Text2Image preset.</summary>
public class T2IPreset
{
    [BsonId]
    public string ID { get; set; }

    /// <summary>The user who made this.</summary>
    public string Author { get; set; }

    /// <summary>User-written title of the preset.</summary>
    public string Title { get; set; }

    /// <summary>User-written description of the preset.</summary>
    public string Description { get; set; }

    /// <summary>Preview image URL for the preset, as a local path, usually within "Output".</summary>
    public string PreviewImage { get; set; }

    /// <summary>Mapping of parameters to values.</summary>
    public Dictionary<string, string> ParamMap { get; set; } = [];

    /// <summary>Gets networkable data about this preset.</summary>
    public JObject NetData()
    {
        return new JObject()
        {
            ["author"] = Author,
            ["title"] = Title,
            ["description"] = Description,
            ["preview_image"] = PreviewImage,
            ["param_map"] = JObject.FromObject(ParamMap)
        };
    }

    /// <summary>Automatically applies the entire preset over top of a <see cref="T2IParams"/> input.</summary>
    public void ApplyTo(T2IParamInput user_input)
    {
        foreach ((string key, string val) in ParamMap)
        {
            if (T2IParamTypes.TryGetType(key, out _, user_input))
            {
                T2IParamTypes.ApplyParameter(key, val, user_input);
            }
            else
            {
                Logs.Warning($"Invalid preset parameter: {key}, for preset '{ID}' created by '{Author}'");
            }
        }
    }
}
