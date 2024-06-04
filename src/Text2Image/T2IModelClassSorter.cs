using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Utils;
using System.IO;

namespace StableSwarmUI.Text2Image;

/// <summary>Helper to determine what classification a model should receive.</summary>
public class T2IModelClassSorter
{
    /// <summary>All known model classes.</summary>
    public static Dictionary<string, T2IModelClass> ModelClasses = [];

    /// <summary>Register a new model class to the sorter.</summary>
    public static void Register(T2IModelClass clazz)
    {
        ModelClasses.Add(clazz.ID, clazz);
    }

    static T2IModelClassSorter()
    {
        bool IsAlt(JObject h) => h.ContainsKey("cond_stage_model.roberta.embeddings.word_embeddings.weight");
        bool isV1(JObject h) => h.ContainsKey("cond_stage_model.transformer.text_model.embeddings.position_ids");
        bool isV2(JObject h) => h.ContainsKey("cond_stage_model.model.ln_final.bias");
        bool isV2Depth(JObject h) => h.ContainsKey("depth_model.model.pretrained.act_postprocess3.0.project.0.bias");
        bool isV2Unclip(JObject h) => h.ContainsKey("embedder.model.visual.transformer.resblocks.0.attn.in_proj_weight");
        bool isXL09Base(JObject h) => h.ContainsKey("conditioner.embedders.0.transformer.text_model.embeddings.position_embedding.weight");
        bool isXL09Refiner(JObject h) => h.ContainsKey("conditioner.embedders.0.model.ln_final.bias");
        bool isv2512name(string name) => name.Contains("512-") || name.Contains("-inpaint") || name.Contains("base-"); // keywords that identify the 512 vs the 768. Unfortunately no good proper detection here, other than execution-based hacks (see Auto WebUI ref)
        bool isControlLora(JObject h) => h.ContainsKey("lora_controlnet");
        bool isTurbo21(JObject h) => h.ContainsKey("denoiser.sigmas");
        Register(new() { ID = "alt_diffusion_v1_512_placeholder", CompatClass = "alt_diffusion_v1", Name = "Alt-Diffusion", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return IsAlt(h);
        }});
        Register(new() { ID = "stable-diffusion-v1", CompatClass = "stable-diffusion-v1", Name = "Stable Diffusion v1", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return isV1(h) && !IsAlt(h) && !isV2(h);
        }});
        Register(new() { ID = "stable-diffusion-v1-inpainting", CompatClass = "stable-diffusion-v1", Name = "Stable Diffusion v1 (Inpainting)", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return false; // TODO: How to detect accurately?
        }});
        Register(new() { ID = "stable-diffusion-v2-512", CompatClass = "stable-diffusion-v2", Name = "Stable Diffusion v2-512", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return isV2(h) && !isV2Unclip(h) && isv2512name(m.Name);
        }});
        Register(new() { ID = "stable-diffusion-v2-768-v", CompatClass = "stable-diffusion-v2", Name = "Stable Diffusion v2-768v", StandardWidth = 768, StandardHeight = 768, IsThisModelOfClass = (m, h) =>
        {
            return isV2(h) && !isV2Unclip(h) && !isv2512name(m.Name);
        }});
        Register(new() { ID = "stable-diffusion-v2-inpainting", CompatClass = "stable-diffusion-v2", Name = "Stable Diffusion v2 (Inpainting)", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return false; // TODO: How to detect accurately?
        }});
        Register(new() { ID = "stable-diffusion-v2-depth", CompatClass = "stable-diffusion-v2", Name = "Stable Diffusion v2 (Depth)", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return isV2Depth(h);
        }});
        Register(new() { ID = "stable-diffusion-v2-unclip", CompatClass = "stable-diffusion-v2", Name = "Stable Diffusion v2 (Unclip)", StandardWidth = 768, StandardHeight = 768, IsThisModelOfClass = (m, h) =>
        {
            return isV2Unclip(h);
        }});
        Register(new() { ID = "stable-diffusion-xl-v0_9-base", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL 0.9-Base", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) =>
        {
            return isXL09Base(h);
        }});
        Register(new() { ID = "stable-diffusion-xl-v0_9-refiner", CompatClass = "stable-diffusion-xl-v1-refiner", Name = "Stable Diffusion XL 0.9-Refiner", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) =>
        {
            return isXL09Refiner(h) && !isTurbo21(h);
        }});
        Register(new() { ID = "stable-diffusion-v2-turbo", CompatClass = "stable-diffusion-v2-turbo", Name = "Stable Diffusion v2 Turbo", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return isTurbo21(h);
        }});
        Register(new() { ID = "stable-diffusion-v1/lora", CompatClass = "stable-diffusion-v1", Name = "Stable Diffusion v1 LoRA", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return h.ContainsKey("lora_unet_up_blocks_3_attentions_2_transformer_blocks_0_ff_net_2.lora_up.weight");
        }});
        Register(new() { ID = "stable-diffusion-v1/controlnet", CompatClass = "stable-diffusion-v1", Name = "Stable Diffusion v1 ControlNet", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            return (h.ContainsKey("input_blocks.1.0.emb_layers.1.bias") || h.ContainsKey("control_model.input_blocks.1.0.emb_layers.1.bias")) && !isControlLora(h);
        }});
        Register(new() { ID = "stable-diffusion-xl-v1-base/lora", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL 1.0-Base LoRA", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) =>
        {
            return h.ContainsKey("lora_unet_output_blocks_5_1_transformer_blocks_1_ff_net_2.lora_up.weight");
        }});
        Register(new() { ID = "stable-diffusion-xl-v1-base/controlnet", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL 1.0-Base ControlNet", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) =>
        {
            return h.ContainsKey("controlnet_down_blocks.0.bias");
        }});
        Register(new() { ID = "stable-video-diffusion-img2vid-v0_9", CompatClass = "stable-video-diffusion-img2vid-v1", Name = "Stable Video Diffusion Img2Vid 0.9", StandardWidth = 1024, StandardHeight = 576, IsThisModelOfClass = (m, h) =>
        {
            return h.ContainsKey("model.diffusion_model.input_blocks.1.0.time_stack.emb_layers.1.bias");
        }});
        JToken GetEmbeddingKey(JObject h)
        {
            if (h.TryGetValue("emb_params", out JToken emb_data))
            {
                return emb_data;
            }
            JProperty[] props = h.Properties().Where(p => p.Name.StartsWith('<') && p.Name.EndsWith('>')).ToArray();
            if (props.Length == 1)
            {
                return props[0].Value;
            }
            return null;
        }
        Register(new() { ID = "stable-diffusion-v1/textual-inversion", CompatClass = "stable-diffusion-v1", Name = "Stable Diffusion v1 Embedding", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) =>
        {
            JToken emb_data = GetEmbeddingKey(h);
            if (emb_data is null || !(emb_data as JObject).TryGetValue("shape", out JToken shape))
            {
                return false;
            }
            return shape.ToArray()[^1].Value<long>() == 768;
        }});
        Register(new() { ID = "stable-diffusion-v2-768-v/textual-inversion", CompatClass = "stable-diffusion-v2", Name = "Stable Diffusion v2 Embedding", StandardWidth = 768, StandardHeight = 768, IsThisModelOfClass = (m, h) =>
        {
            JToken emb_data = GetEmbeddingKey(h);
            if (emb_data is null)
            {
                return false;
            }
            if (emb_data is null || !(emb_data as JObject).TryGetValue("shape", out JToken shape))
            {
                return false;
            }
            return shape.ToArray()[^1].Value<long>() == 1024;
        }
        });
        Register(new() { ID = "stable-diffusion-xl-v1-base/textual-inversion", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL 1.0-Base Embedding", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) =>
        {
            return h.TryGetValue("clip_g", out JToken clip_g) && (clip_g as JObject).TryGetValue("shape", out JToken shape_g) && shape_g[1].Value<long>() == 1280
                && h.TryGetValue("clip_l", out JToken clip_l) && (clip_l as JObject).TryGetValue("shape", out JToken shape_l) && shape_l[1].Value<long>() == 768;
        }});
        // Everything below this point does not autodetect, it must match through ModelSpec
        Register(new() { ID = "stable-diffusion-v1/vae", CompatClass = "stable-diffusion-v1", Name = "Stable Diffusion v1 VAE", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-v1/inpaint", CompatClass = "stable-diffusion-v1", Name = "Stable Diffusion v1 (Inpainting)", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-v2-768-v/lora", CompatClass = "stable-diffusion-v2", Name = "Stable Diffusion v2 LoRA", StandardWidth = 768, StandardHeight = 768, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-xl-v1-base", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL 1.0-Base", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; }});
        Register(new() { ID = "stable-diffusion-xl-turbo-v1", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL Turbo", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-xl-v1-refiner", CompatClass = "stable-diffusion-xl-v1-refiner", Name = "Stable Diffusion XL 1.0-Refiner", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-xl-v1-base/vae", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL 1.0-Base VAE", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-xl-v1-edit", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL 1.0 Edit", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-xl-v1-base/control-lora", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL 1.0-Base Control-LoRA", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) =>
        {
            return isControlLora(h);
        }});
        Register(new() { ID = "segmind-stable-diffusion-1b", CompatClass = "segmind-stable-diffusion-1b", Name = "Segmind Stable Diffusion 1B (SSD-1B)", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-video-diffusion-img2vid-v1", CompatClass = "stable-video-diffusion-img2vid-v1", Name = "Stable Video Diffusion Img2Vid v1", StandardWidth = 1024, StandardHeight = 576, IsThisModelOfClass = (m, h) => { return false; }});
        Register(new() { ID = "stable-cascade-v1-stage-a", CompatClass = "stable-cascade-v1", Name = "Stable Cascade v1 (Stage A)", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-cascade-v1-stage-b", CompatClass = "stable-cascade-v1", Name = "Stable Cascade v1 (Stage B)", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-cascade-v1-stage-c", CompatClass = "stable-cascade-v1", Name = "Stable Cascade v1 (Stage C)", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-v3-medium", CompatClass = "stable-diffusion-v3-medium", Name = "Stable Diffusion 3 Medium", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; } });
        // TensorRT variants
        Register(new() { ID = "stable-diffusion-v1/tensorrt", CompatClass = "stable-diffusion-v1", Name = "Stable Diffusion v1 (TensorRT Engine)", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-v2-768-v/tensorrt", CompatClass = "stable-diffusion-v2", Name = "Stable Diffusion v2 (TensorRT Engine)", StandardWidth = 768, StandardHeight = 768, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-xl-v0_9-base/tensorrt", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL 0.9-Base (TensorRT Engine)", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-xl-v1-base/tensorrt", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL 1.0-Base (TensorRT Engine)", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-xl-turbo-v1/tensorrt", CompatClass = "stable-diffusion-xl-v1", Name = "Stable Diffusion XL Turbo (TensorRT Engine)", StandardWidth = 512, StandardHeight = 512, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-diffusion-xl-v1-refiner/tensorrt", CompatClass = "stable-diffusion-xl-v1-refiner", Name = "Stable Diffusion XL 1.0-Refiner (TensorRT Engine)", StandardWidth = 1024, StandardHeight = 1024, IsThisModelOfClass = (m, h) => { return false; } });
        Register(new() { ID = "stable-video-diffusion-img2vid-v1/tensorrt", CompatClass = "stable-video-diffusion-img2vid-v1", Name = "Stable Video Diffusion Img2Vid v1 (TensorRT Engine)", StandardWidth = 1024, StandardHeight = 576, IsThisModelOfClass = (m, h) => { return false; } });
    }

    /// <summary>Returns the model class that matches this model, or null if none.</summary>
    public static T2IModelClass IdentifyClassFor(T2IModel model, JObject header)
    {
        if (model.ModelClass is not null)
        {
            return model.ModelClass;
        }
        string arch = header?["__metadata__"]?.Value<string>("modelspec.architecture") ?? header?["__metadata__"]?.Value<string>("architecture") ?? header.Value<string>("architecture");
        if (arch is not null)
        {
            string res = header["__metadata__"]?.Value<string>("modelspec.resolution") ?? header["__metadata__"]?.Value<string>("resolution") ?? header.Value<string>("resolution");
            string h = null;
            int width = string.IsNullOrWhiteSpace(res) ? 0 : int.Parse(res.BeforeAndAfter('x', out h));
            int height = string.IsNullOrWhiteSpace(h) ? 0 : int.Parse(h);
            if (ModelClasses.TryGetValue(arch, out T2IModelClass clazz))
            {
                if ((width == clazz.StandardWidth && height == clazz.StandardHeight) || (width <= 0 && height <= 0))
                {
                    Logs.Debug($"Model {model.Name} matches {clazz.Name} by architecture ID");
                    return clazz;
                }
                else
                {
                    Logs.Debug($"Model {model.Name} matches {clazz.Name} by architecture ID, but resolution is different ({width}x{height} vs {clazz.StandardWidth}x{clazz.StandardHeight})");
                    return clazz with { StandardWidth = width, StandardHeight = height, IsThisModelOfClass = (m, h) => false };
                }
            }
            Logs.Debug($"Model {model.Name} has unknown architecture ID {arch}");
            return new() { ID = arch, CompatClass = arch, Name = arch, StandardWidth = width, StandardHeight = height, IsThisModelOfClass = (m, h) => false };
        }
        if (!model.RawFilePath.EndsWith(".safetensors") && header is null)
        {
            Logs.Debug($"Model {model.Name} cannot have known type, not safetensors and no header");
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
