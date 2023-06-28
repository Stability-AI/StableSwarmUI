using Newtonsoft.Json.Linq;
using System.IO;

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

    /// <summary>What class this model is, if known.</summary>
    public T2IModelClass ModelClass;

    /// <summary>Gets a networkable copy of this model's data.</summary>
    public JObject ToNetObject()
    {
        return new JObject()
        {
            ["name"] = Name,
            ["description"] = Description,
            ["type"] = Type,
            ["preview_image"] = PreviewImage,
            ["loaded"] = AnyBackendsHaveLoaded,
            ["class"] = ModelClass?.Name,
            ["standard_width"] = ModelClass?.StandardWidth ?? 0,
            ["standard_height"] = ModelClass?.StandardHeight ?? 0,
        };
    }

    /// <summary>Get the safetensors header from a model.</summary>
    public static string GetSafetensorsHeaderFrom(string modelPath)
    {
        using FileStream file = File.OpenRead(modelPath);
        byte[] lenBuf = new byte[8];
        file.ReadExactly(lenBuf, 0, 8);
        long len = BitConverter.ToInt64(lenBuf, 0);
        if (len < 0 || len > 100 * 1024 * 1024)
        {
            throw new InvalidOperationException($"Improper safetensors file. Wrong file type, or unreasonable header length: {len}");
        }
        byte[] dataBuf = new byte[len];
        file.ReadExactly(dataBuf, 0, (int)len);
        return Encoding.UTF8.GetString(dataBuf);
    }
}
