using Newtonsoft.Json.Linq;
using StableSwarmUI.Utils;
using System.IO;

namespace StableSwarmUI.Text2Image;

/// <summary>Helper to determine what classification a model should receive.</summary>
public class T2IModelClassSorter
{
    /// <summary>All known model classes.</summary>
    public Dictionary<string, T2IModelClass> ModelClasses = new();

    /// <summary>Register a new model class to the sorter.</summary>
    public void Register(T2IModelClass clazz)
    {
        ModelClasses.Add(clazz.Name, clazz);
    }

    public T2IModelClassSorter()
    {
        // TODO: These are hacks. There needs to be standard metadata to identify model type!!!
        bool IsAlt(JObject h) => h.ContainsKey("cond_stage_model.roberta.embeddings.word_embeddings.weight");
        bool isV1(JObject h) => h.ContainsKey("cond_stage_model.transformer.text_model.embeddings.position_ids");
        bool isV2(JObject h) => h.ContainsKey("cond_stage_model.model.ln_final.bias");
        bool isV2Depth(JObject h) => h.ContainsKey("depth_model.model.pretrained.act_postprocess3.0.project.0.bias");
        bool isV2Unclip(JObject h) => h.ContainsKey("embedder.model.visual.transformer.resblocks.0.attn.in_proj_weight");
        bool isXL09Base(JObject h) => h.ContainsKey("conditioner.embedders.0.transformer.text_model.embeddings.position_embedding.weight");
        bool isXL09Refiner(JObject h) => h.ContainsKey("conditioner.embedders.0.model.ln_final.bias");
        bool isv2512name(string name) => name.Contains("512-") || name.Contains("-inpaint") || name.Contains("base-"); // keywords that identify the 512 vs the 768. Unfortunately no good proper detection here, other than execution-based hacks (see Auto WebUI ref)
        Register(new() { Name = "Alt-Diffusion", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return IsAlt(h);
        }});
        Register(new() { Name = "Stable Diffusion v1", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return isV1(h) && !IsAlt(h) && !isV2(h);
        }});
        Register(new() { Name = "Stable Diffusion v2-512", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return isV2(h) && !isV2Unclip(h) && isv2512name(m.Name);
        }});
        Register(new() { Name = "Stable Diffusion v2-768v", StandardWidth = 768, StandardHeight = 768, IsThisModelOfClass = (m, h) =>
        {
            return isV2(h) && !isV2Unclip(h) && !isv2512name(m.Name);
        }});
        Register(new() { Name = "Stable Diffusion v2 (Depth)", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return isV2Depth(h);
        }});
        Register(new() { Name = "Stable Diffusion v2 (Unclip)", StandardWidth = 768, StandardHeight = 768, IsThisModelOfClass = (m, h) =>
        {
            return isV2Unclip(h);
        }});
        Register(new() { Name = "Stable Diffusion XL 0.9-Base", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) =>
        {
            return isXL09Base(h);
        }});
        Register(new() { Name = "Stable Diffusion XL 0.9-Refiner", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) =>
        {
            return isXL09Refiner(h);
        }});
    }

    /// <summary>Returns the model class that matches this model, or null if none.</summary>
    public T2IModelClass IdentifyClassFor(T2IModel model, JObject header)
    {
        if (model.ModelClass is not null)
        {
            return model.ModelClass;
        }
        if (!model.RawFilePath.EndsWith(".safetensors") || header is null)
        {
            Logs.Debug($"Model {model.Name} cannot have known type, not safetensors or no header");
            return null;
        }
        foreach (T2IModelClass modelClass in ModelClasses.Values)
        {
            if (modelClass.IsThisModelOfClass(model, header))
            {
                Logs.Debug($"Model {model.Name} seems to match type {modelClass.Name}");
                return modelClass;
            }
        }
        Logs.Debug($"Model {model.Name} did not match any of {ModelClasses.Count} options");
        return null;
    }
}
