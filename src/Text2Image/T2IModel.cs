using Newtonsoft.Json.Linq;
using System.IO;

namespace StableSwarmUI.Text2Image;

/// <summary>Represents the basic data around an (unloaded) Text2Image model.</summary>
public class T2IModel
{
    /// <summary>Name/ID of the model.</summary>
    public string Name;

    /// <summary>Full raw system filepath to this model.</summary>
    public string RawFilePath;

    /// <summary>Proper title of the model, if identified.</summary>
    public string Title;

    /// <summary>Description text, if any, of the model.</summary>
    public string Description;

    /// <summary>URL or data blob of a preview image for this model.</summary>
    public string PreviewImage;

    /// <summary>This model's standard resolution, eg 1024x1024. 0 means unknown.</summary>
    public int StandardWidth, StandardHeight;

    /// <summary>If true, at least one backend has this model currently loaded.</summary>
    public bool AnyBackendsHaveLoaded = false;

    /// <summary>What class this model is, if known.</summary>
    public T2IModelClass ModelClass;

    /// <summary>Metadata about this model.</summary>
    public T2IModelHandler.ModelMetadataStore Metadata;

    /// <summary>Gets a networkable copy of this model's data.</summary>
    public JObject ToNetObject()
    {
        return new JObject()
        {
            ["name"] = Name,
            ["title"] = Metadata?.Title,
            ["author"] = Metadata?.Author,
            ["description"] = Description,
            ["model_class"] = ModelClass?.Name,
            ["preview_image"] = PreviewImage,
            ["loaded"] = AnyBackendsHaveLoaded,
            ["architecture"] = ModelClass?.ID,
            ["class"] = ModelClass?.Name,
            ["standard_width"] = StandardWidth,
            ["standard_height"] = StandardHeight,
            ["license"] = Metadata?.License,
            ["date"] = Metadata?.Date,
            ["usage_hint"] = Metadata?.UsageHint,
            ["trigger_phrase"] = Metadata?.TriggerPhrase,
            ["merged_from"] = Metadata?.MergedFrom,
            ["tags"] = Metadata?.Tags is null ? null : new JArray(Metadata.Tags),
            ["is_safetensors"] = RawFilePath.EndsWith(".safetensors")
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
            throw new InvalidOperationException($"Improper safetensors file {modelPath}. Wrong file type, or unreasonable header length: {len}");
        }
        byte[] dataBuf = new byte[len];
        file.ReadExactly(dataBuf, 0, (int)len);
        return Encoding.UTF8.GetString(dataBuf);
    }

    /// <summary>Returns the name of the model.</summary>
    public string ToString(string folderFormat)
    {
        return Name.Replace("/", folderFormat ?? $"{Path.DirectorySeparatorChar}");
    }

    /// <summary>Returns the name of the model.</summary>
    public override string ToString()
    {
        return Name.Replace('/', Path.DirectorySeparatorChar);
    }
}
