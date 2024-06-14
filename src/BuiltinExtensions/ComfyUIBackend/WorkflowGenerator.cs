
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.IO;

namespace StableSwarmUI.Builtin_ComfyUIBackend;

/// <summary>Helper class for generating ComfyUI workflows from input parameters.</summary>
public class WorkflowGenerator
{
    /// <summary>Represents a step in the workflow generation process.</summary>
    /// <param name="Action">The action to take.</param>
    /// <param name="Priority">The priority to apply it at.
    /// These are such from lowest to highest.
    /// "-10" is the priority of the first core pre-init,
    /// "0" is before final outputs,
    /// "10" is final output.</param>
    public record class WorkflowGenStep(Action<WorkflowGenerator> Action, double Priority);

    /// <summary>Callable steps for modifying workflows as they go.</summary>
    public static List<WorkflowGenStep> Steps = [];

    /// <summary>Callable steps for configuring model generation.</summary>
    public static List<WorkflowGenStep> ModelGenSteps = [];

    /// <summary>Can be set to globally block custom nodes, if needed.</summary>
    public static volatile bool RestrictCustomNodes = false;

    /// <summary>Supported Features of the comfy backend.</summary>
    public HashSet<string> Features = [];

    /// <summary>Helper tracker for CLIP Models that are loaded (to skip a datadrive read from being reused every time).</summary>
    public static HashSet<string> ClipModelsValid = [];

    /// <summary>Helper tracker for Vision Models that are loaded (to skip a datadrive read from being reused every time).</summary>
    public static HashSet<string> VisionModelsValid = [];

    /// <summary>Helper tracker for IP Adapter Models that are loaded (to skip a datadrive read from being reused every time).</summary>
    public static HashSet<string> IPAdapterModelsValid = [];

    /// <summary>Register a new step to the workflow generator.</summary>
    public static void AddStep(Action<WorkflowGenerator> step, double priority)
    {
        Steps.Add(new(step, priority));
        Steps = [.. Steps.OrderBy(s => s.Priority)];
    }

    /// <summary>Register a new step to the workflow generator.</summary>
    public static void AddModelGenStep(Action<WorkflowGenerator> step, double priority)
    {
        ModelGenSteps.Add(new(step, priority));
        ModelGenSteps = [.. ModelGenSteps.OrderBy(s => s.Priority)];
    }

    /// <summary>Lock for when ensuring the backend has valid models.</summary>
    public static LockObject ModelDownloaderLock = new();

    /* ========= RESERVED NODES ID MAP =========
     * 4: Initial Model Loader
     * 5: VAE Encode Init or Empty Latent
     * 6: Positive Prompt
     * 7: Negative Prompt
     * 8: Final VAEDecode
     * 9: Final Image Save
     * 10: Main KSampler
     * 11: Alternate Main VAE Loader
     * 15: Image Load
     * 20: Refiner Model Loader
     * 21: Refiner VAE Loader
     * 23: Refiner KSampler
     * 24: Refiner VAEDecoder
     * 25: Refiner VAEEncode
     * 26: Refiner ImageScale
     * 27: Refiner UpscaleModelLoader
     * 28: Refiner ImageUpscaleWithModel
     * 29: Refiner ImageSave
     *
     * 100+: Dynamic
     * 1500+: LoRA Loaders (Stable-Dynamic)
     * 50,000+: Intermediate Image Saves (Stable-Dynamic)
     */

    static WorkflowGenerator()
    {
        #region Model Loader
        AddStep(g =>
        {
            g.FinalLoadedModel = g.UserInput.Get(T2IParamTypes.Model);
            (g.FinalLoadedModel, g.FinalModel, g.FinalClip, g.FinalVae) = g.CreateStandardModelLoader(g.FinalLoadedModel, "Base", "4");
        }, -15);
        AddModelGenStep(g =>
        {
            if (g.IsRefinerStage && g.UserInput.TryGet(T2IParamTypes.RefinerVAE, out T2IModel rvae))
            {
                string vaeNode = g.CreateNode("VAELoader", new JObject()
                {
                    ["vae_name"] = rvae.ToString(g.ModelFolderFormat)
                }, g.HasNode("21") ? null : "21");
                g.LoadingVAE = [vaeNode, 0];
            }
            else if (!g.NoVAEOverride && g.UserInput.TryGet(T2IParamTypes.VAE, out T2IModel vae))
            {
                if (g.FinalLoadedModel.ModelClass?.ID == "stable-diffusion-v3-medium")
                {
                    Logs.Warning($"WARNING: Model {g.FinalLoadedModel.Title} is an SD3 model, but you have VAE {vae.Title} selected. If that VAE is not an SD3 specific VAE, this is likely a mistake. Errors may follow. If this breaks, disable the custom VAE.");
                }
                string vaeNode = g.CreateNode("VAELoader", new JObject()
                {
                    ["vae_name"] = vae.ToString(g.ModelFolderFormat)
                }, g.HasNode("11") ? null : "11");
                g.LoadingVAE = [vaeNode, 0];
            }
        }, -14);
        AddModelGenStep(g =>
        {
            (g.LoadingModel, g.LoadingClip) = g.LoadLorasForConfinement(0, g.LoadingModel, g.LoadingClip);
        }, -10);
        AddModelGenStep(g =>
        {
            string applyTo = g.UserInput.Get(T2IParamTypes.FreeUApplyTo, null);
            if (ComfyUIBackendExtension.FeaturesSupported.Contains("freeu") && applyTo is not null)
            {
                if (applyTo == "Both" || applyTo == g.LoadingModelType)
                {
                    string freeU = g.CreateNode("FreeU", new JObject()
                    {
                        ["model"] = g.LoadingModel,
                        ["b1"] = g.UserInput.Get(T2IParamTypes.FreeUBlock1),
                        ["b2"] = g.UserInput.Get(T2IParamTypes.FreeUBlock2),
                        ["s1"] = g.UserInput.Get(T2IParamTypes.FreeUSkip1),
                        ["s2"] = g.UserInput.Get(T2IParamTypes.FreeUSkip2)
                    });
                    g.LoadingModel = [freeU, 0];
                }
            }
        }, -8);
        AddModelGenStep(g =>
        {
            if (g.UserInput.TryGet(ComfyUIBackendExtension.SelfAttentionGuidanceScale, out double sagScale))
            {
                string guided = g.CreateNode("SelfAttentionGuidance", new JObject()
                {
                    ["model"] = g.LoadingModel,
                    ["scale"] = sagScale,
                    ["blur_sigma"] = g.UserInput.Get(ComfyUIBackendExtension.SelfAttentionGuidanceSigmaBlur, 2.0)
                });
                g.LoadingModel = [guided, 0];
            }
        }, -7);
        AddModelGenStep(g =>
        {
            if (g.UserInput.TryGet(T2IParamTypes.ClipStopAtLayer, out int layer))
            {
                string clipSkip = g.CreateNode("CLIPSetLastLayer", new JObject()
                {
                    ["clip"] = g.LoadingClip,
                    ["stop_at_clip_layer"] = layer
                });
                g.LoadingClip = [clipSkip, 0];
            }
        }, -6);
        AddModelGenStep(g =>
        {
            if (g.UserInput.TryGet(T2IParamTypes.SeamlessTileable, out string tileable) && tileable != "false")
            {
                string mode = "Both";
                if (tileable == "X-Only") { mode = "X"; }
                else if (tileable == "Y-Only") { mode = "Y"; }
                string tiling = g.CreateNode("SwarmModelTiling", new JObject()
                {
                    ["model"] = g.LoadingModel,
                    ["tile_axis"] = mode
                });
                g.LoadingModel = [tiling, 0];
                string tilingVae = g.CreateNode("SwarmTileableVAE", new JObject()
                {
                    ["vae"] = g.LoadingVAE,
                    ["tile_axis"] = mode
                });
                g.LoadingVAE = [tilingVae, 0];
            }
        }, -5);
        AddModelGenStep(g =>
        {
            if (ComfyUIBackendExtension.FeaturesSupported.Contains("aitemplate") && g.UserInput.Get(ComfyUIBackendExtension.AITemplateParam))
            {
                string aitLoad = g.CreateNode("AITemplateLoader", new JObject()
                {
                    ["model"] = g.LoadingModel,
                    ["keep_loaded"] = "disable"
                });
                g.LoadingModel = [aitLoad, 0];
            }
        }, -3);
        #endregion
        #region Base Image
        AddStep(g =>
        {
            if (g.UserInput.TryGet(T2IParamTypes.InitImage, out Image img))
            {
                string maskImageNode = null;
                if (g.UserInput.TryGet(T2IParamTypes.MaskImage, out Image mask))
                {
                    string maskNode = g.CreateLoadImageNode(mask, "${maskimage}", true);
                    maskImageNode = g.CreateNode("ImageToMask", new JObject()
                    {
                        ["image"] = new JArray() { maskNode, 0 },
                        ["channel"] = "red"
                    });
                    g.EnableDifferential();
                    if (g.UserInput.TryGet(T2IParamTypes.MaskBlur, out int blurAmount))
                    {
                        maskImageNode = g.CreateNode("SwarmMaskBlur", new JObject()
                        {
                            ["mask"] = new JArray() { maskImageNode, 0 },
                            ["blur_radius"] = blurAmount,
                            ["sigma"] = 1.0
                        });
                    }
                    g.FinalMask = [maskImageNode, 0];
                }
                g.CreateLoadImageNode(img, "${initimage}", true, "15");
                g.FinalInputImage = ["15", 0];
                g.CreateVAEEncode(g.FinalVae, ["15", 0], "5", mask: g.FinalMask);
                if (g.UserInput.TryGet(T2IParamTypes.UnsamplerPrompt, out string unprompt))
                {
                    int steps = g.UserInput.Get(T2IParamTypes.Steps);
                    int startStep = 0;
                    if (g.UserInput.TryGet(T2IParamTypes.InitImageCreativity, out double creativity))
                    {
                        startStep = (int)Math.Round(steps * (1 - creativity));
                    }
                    JArray posCond = g.CreateConditioning(unprompt, g.FinalClip, g.FinalLoadedModel, true);
                    JArray negCond = g.CreateConditioning(g.UserInput.Get(T2IParamTypes.NegativePrompt, ""), g.FinalClip, g.FinalLoadedModel, false);
                    string unsampler = g.CreateNode("SwarmUnsampler", new JObject()
                    {
                        ["model"] = g.FinalModel,
                        ["steps"] = steps,
                        ["sampler_name"] = g.UserInput.Get(ComfyUIBackendExtension.SamplerParam, "euler"),
                        ["scheduler"] = g.UserInput.Get(ComfyUIBackendExtension.SchedulerParam, "normal"),
                        ["positive"] = posCond,
                        ["negative"] = negCond,
                        ["latent_image"] = g.FinalLatentImage,
                        ["start_at_step"] = startStep,
                        ["previews"] = g.UserInput.Get(T2IParamTypes.NoPreviews) ? "none" : "default"
                    });
                    g.FinalLatentImage = [unsampler, 0];
                    g.MainSamplerAddNoise = false;
                }
                if (g.UserInput.TryGet(T2IParamTypes.BatchSize, out int batchSize) && batchSize > 1)
                {
                    string batchNode = g.CreateNode("RepeatLatentBatch", new JObject()
                    {
                        ["samples"] = g.FinalLatentImage,
                        ["amount"] = batchSize
                    });
                    g.FinalLatentImage = [batchNode, 0];
                }
                if (g.UserInput.TryGet(T2IParamTypes.InitImageResetToNorm, out double resetFactor))
                {
                    g.InitialImageIsAlteredAsLatent = true;
                    string emptyImg = g.CreateEmptyImage(g.UserInput.Get(T2IParamTypes.Width), g.UserInput.GetImageHeight(), g.UserInput.Get(T2IParamTypes.BatchSize, 1));
                    if (g.Features.Contains("comfy_latent_blend_masked") && g.FinalMask is not null)
                    {
                        string blended = g.CreateNode("SwarmLatentBlendMasked", new JObject()
                        {
                            ["samples0"] = g.FinalLatentImage,
                            ["samples1"] = new JArray() { emptyImg, 0 },
                            ["mask"] = g.FinalMask,
                            ["blend_factor"] = resetFactor
                        });
                        g.FinalLatentImage = [blended, 0];
                    }
                    else
                    {
                        string emptyMultiplied = g.CreateNode("LatentMultiply", new JObject()
                        {
                            ["samples"] = new JArray() { emptyImg, 0 },
                            ["multiplier"] = resetFactor
                        });
                        string originalMultiplied = g.CreateNode("LatentMultiply", new JObject()
                        {
                            ["samples"] = g.FinalLatentImage,
                            ["multiplier"] = 1 - resetFactor
                        });
                        string added = g.CreateNode("LatentAdd", new JObject()
                        {
                            ["samples1"] = new JArray() { emptyMultiplied, 0 },
                            ["samples2"] = new JArray() { originalMultiplied, 0 }
                        });
                        g.FinalLatentImage = [added, 0];
                    }
                }
                if (g.FinalMask is not null)
                {
                    if (g.UserInput.TryGet(T2IParamTypes.MaskShrinkGrow, out int shrinkGrow))
                    {
                        if (g.InitialImageIsAlteredAsLatent)
                        {
                            string decoded = g.CreateVAEDecode(g.FinalVae, g.FinalLatentImage);
                            g.FinalInputImage = [decoded, 0];
                            g.InitialImageIsAlteredAsLatent = false;
                        }
                        g.MaskShrunkInfo = g.CreateImageMaskCrop(g.FinalMask, g.FinalInputImage, shrinkGrow, g.FinalVae);
                        g.FinalLatentImage = [g.MaskShrunkInfo.Item3, 0];
                    }
                    else
                    {
                        string appliedNode = g.CreateNode("SetLatentNoiseMask", new JObject()
                        {
                            ["samples"] = g.FinalLatentImage,
                            ["mask"] = g.FinalMask
                        });
                        g.FinalLatentImage = [appliedNode, 0];
                    }
                }
            }
            else
            {
                g.CreateEmptyImage(g.UserInput.Get(T2IParamTypes.Width), g.UserInput.GetImageHeight(), g.UserInput.Get(T2IParamTypes.BatchSize, 1), "5");
            }
        }, -9);
        #endregion
        #region Positive Prompt
        AddStep(g =>
        {
            g.FinalPrompt = g.CreateConditioning(g.UserInput.Get(T2IParamTypes.Prompt), g.FinalClip, g.UserInput.Get(T2IParamTypes.Model), true, "6");
        }, -8);
        #endregion
        #region ReVision/UnCLIP/IPAdapter
        AddStep(g =>
        {
            if (g.UserInput.TryGet(T2IParamTypes.PromptImages, out List<Image> images) && images.Any())
            {
                void requireVisionModel(string name, string url)
                {
                    if (VisionModelsValid.Contains(name))
                    {
                        return;
                    }
                    string filePath = Utilities.CombinePathWithAbsolute(Program.ServerSettings.Paths.ModelRoot, Program.ServerSettings.Paths.SDClipVisionFolder, name);
                    g.DownloadModel(name, filePath, url);
                    VisionModelsValid.Add(name);
                }
                string visModelName = "clip_vision_g.safetensors";
                if (g.UserInput.TryGet(T2IParamTypes.ReVisionModel, out T2IModel visionModel))
                {
                    visModelName = visionModel.ToString(g.ModelFolderFormat);
                }
                else
                {
                    requireVisionModel(visModelName, "https://huggingface.co/stabilityai/control-lora/resolve/main/revision/clip_vision_g.safetensors");
                }
                string visionLoader = g.CreateNode("CLIPVisionLoader", new JObject()
                {
                    ["clip_name"] = visModelName
                });
                double revisionStrength = g.UserInput.Get(T2IParamTypes.ReVisionStrength, 1);
                if (revisionStrength > 0)
                {
                    bool autoZero = g.UserInput.Get(T2IParamTypes.RevisionZeroPrompt, false);
                    if ((g.UserInput.TryGet(T2IParamTypes.Prompt, out string promptText) && string.IsNullOrWhiteSpace(promptText)) || autoZero)
                    {
                        string zeroed = g.CreateNode("ConditioningZeroOut", new JObject()
                        {
                            ["conditioning"] = g.FinalPrompt
                        });
                        g.FinalPrompt = [zeroed, 0];
                    }
                    if ((g.UserInput.TryGet(T2IParamTypes.NegativePrompt, out string negPromptText) && string.IsNullOrWhiteSpace(negPromptText)) || autoZero)
                    {
                        string zeroed = g.CreateNode("ConditioningZeroOut", new JObject()
                        {
                            ["conditioning"] = g.FinalNegativePrompt
                        });
                        g.FinalNegativePrompt = [zeroed, 0];
                    }
                    if (!g.UserInput.TryGet(T2IParamTypes.Model, out T2IModel model) || model.ModelClass is null || model.ModelClass.ID != "stable-diffusion-xl-v1-base")
                    {
                        throw new InvalidDataException($"Model type must be stable-diffusion-xl-v1-base for ReVision (currently is {model?.ModelClass?.ID ?? "Unknown"}). Set ReVision Strength to 0 if you just want IP-Adapter.");
                    }
                    for (int i = 0; i < images.Count; i++)
                    {
                        string imageLoader = g.CreateLoadImageNode(images[i], "${promptimages." + i + "}", false);
                        string encoded = g.CreateNode("CLIPVisionEncode", new JObject()
                        {
                            ["clip_vision"] = new JArray() { $"{visionLoader}", 0 },
                            ["image"] = new JArray() { $"{imageLoader}", 0 }
                        });
                        string unclipped = g.CreateNode("unCLIPConditioning", new JObject()
                        {
                            ["conditioning"] = g.FinalPrompt,
                            ["clip_vision_output"] = new JArray() { $"{encoded}", 0 },
                            ["strength"] = revisionStrength,
                            ["noise_augmentation"] = 0
                        });
                        g.FinalPrompt = [unclipped, 0];
                    }
                }
                if (g.UserInput.Get(T2IParamTypes.UseReferenceOnly, false))
                {
                    string firstImg = g.CreateLoadImageNode(images[0], "${promptimages.0}", true);
                    string lastVae = g.CreateVAEEncode(g.FinalVae, [firstImg, 0]);
                    for (int i = 1; i < images.Count; i++)
                    {
                        string newImg = g.CreateLoadImageNode(images[i], "${promptimages." + i + "}", true);
                        string newVae = g.CreateVAEEncode(g.FinalVae, [newImg, 0]);
                        lastVae = g.CreateNode("LatentBatch", new JObject()
                        {
                            ["samples1"] = new JArray() { lastVae, 0 },
                            ["samples2"] = new JArray() { newVae, 0 }
                        });
                    }
                    string referencedModel = g.CreateNode("SwarmReferenceOnly", new JObject()
                    {
                        ["model"] = g.FinalModel,
                        ["reference"] = new JArray() { lastVae, 0 },
                        ["latent"] = g.FinalLatentImage
                    });
                    g.FinalModel = [referencedModel, 0];
                    g.FinalLatentImage = [referencedModel, 1];
                    g.DefaultPreviews = "second";
                }
                if (g.UserInput.TryGet(ComfyUIBackendExtension.UseIPAdapterForRevision, out string ipAdapter) && ipAdapter != "None")
                {
                    string ipAdapterVisionLoader = visionLoader;
                    if (g.Features.Contains("cubiqipadapterunified"))
                    {
                        requireVisionModel("CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors", "https://huggingface.co/h94/IP-Adapter/resolve/main/models/image_encoder/model.safetensors");
                        requireVisionModel("CLIP-ViT-bigG-14-laion2B-39B-b160k.safetensors", "https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/image_encoder/model.safetensors");
                    }
                    else
                    {
                        if ((ipAdapter.Contains("sd15") && !ipAdapter.Contains("vit-G")) || ipAdapter.Contains("vit-h"))
                        {
                            string targetName = "clip_vision_h.safetensors";
                            requireVisionModel(targetName, "https://huggingface.co/h94/IP-Adapter/resolve/main/models/image_encoder/model.safetensors");
                            ipAdapterVisionLoader = g.CreateNode("CLIPVisionLoader", new JObject()
                            {
                                ["clip_name"] = targetName
                            });
                        }
                    }
                    string lastImage = g.CreateLoadImageNode(images[0], "${promptimages.0}", false);
                    for (int i = 1; i < images.Count; i++)
                    {
                        string newImg = g.CreateLoadImageNode(images[i], "${promptimages." + i + "}", false);
                        lastImage = g.CreateNode("ImageBatch", new JObject()
                        {
                            ["image1"] = new JArray() { lastImage, 0 },
                            ["image2"] = new JArray() { newImg, 0 }
                        });
                    }
                    if (g.Features.Contains("cubiqipadapterunified"))
                    {
                        string presetLow = ipAdapter.ToLowerFast();
                        bool isXl = g.CurrentCompatClass() == "stable-diffusion-xl-v1";
                        void requireIPAdapterModel(string name, string url)
                        {
                            if (IPAdapterModelsValid.Contains(name))
                            {
                                return;
                            }
                            string filePath = Utilities.CombinePathWithAbsolute(Program.ServerSettings.Paths.ModelRoot, $"ipadapter/{name}");
                            g.DownloadModel(name, filePath, url);
                            IPAdapterModelsValid.Add(name);
                        }
                        void requireLora(string name, string url)
                        {
                            if (IPAdapterModelsValid.Contains($"LORA-{name}"))
                            {
                                return;
                            }
                            string filePath = Utilities.CombinePathWithAbsolute(Program.ServerSettings.Paths.ModelRoot, Program.ServerSettings.Paths.SDLoraFolder, $"ipadapter/{name}");
                            g.DownloadModel(name, filePath, url);
                            IPAdapterModelsValid.Add($"LORA-{name}");
                        }
                        if (presetLow.StartsWith("light"))
                        {
                            if (isXl) { throw new InvalidOperationException("IP-Adapter light model is not supported for SDXL"); }
                            else { requireIPAdapterModel("sd15_light_v11.bin", "https://huggingface.co/h94/IP-Adapter/resolve/main/models/ip-adapter_sd15_light_v11.bin"); }
                        }
                        else if (presetLow.StartsWith("standard"))
                        {
                            if (isXl) { requireIPAdapterModel("ip-adapter_sdxl_vit-h.safetensors", "https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter_sdxl_vit-h.safetensors"); }
                            else { requireIPAdapterModel("ip-adapter_sd15.safetensors", "https://huggingface.co/h94/IP-Adapter/resolve/main/models/ip-adapter_sd15.safetensors"); }
                        }
                        else if (presetLow.StartsWith("vit-g"))
                        {
                            if (isXl) { requireIPAdapterModel("ip-adapter_sdxl.safetensors", "https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter_sdxl.safetensors"); }
                            else { requireIPAdapterModel("ip-adapter_sd15_vit-G.safetensors", "https://huggingface.co/h94/IP-Adapter/resolve/main/models/ip-adapter_sd15_vit-G.safetensors"); }
                        }
                        else if (presetLow.StartsWith("plus ("))
                        {
                            if (isXl) { requireIPAdapterModel("ip-adapter-plus_sdxl_vit-h.safetensors", "https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter-plus_sdxl_vit-h.safetensors"); }
                            else { requireIPAdapterModel("ip-adapter-plus_sd15.safetensors", "https://huggingface.co/h94/IP-Adapter/resolve/main/models/ip-adapter-plus_sd15.safetensors"); }
                        }
                        else if (presetLow.StartsWith("plus face"))
                        {
                            if (isXl) { requireIPAdapterModel("ip-adapter-plus-face_sdxl_vit-h.safetensors", "https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter-plus-face_sdxl_vit-h.safetensors"); }
                            else { requireIPAdapterModel("ip-adapter-plus-face_sd15.safetensors", "https://huggingface.co/h94/IP-Adapter/resolve/main/models/ip-adapter-plus-face_sd15.safetensors"); }
                        }
                        else if (presetLow.StartsWith("full"))
                        {
                            if (isXl) { throw new InvalidOperationException("IP-Adapter full face model is not supported for SDXL"); }
                            else { requireIPAdapterModel("full_face_sd15.safetensors", "https://huggingface.co/h94/IP-Adapter/resolve/main/models/ip-adapter-full-face_sd15.safetensors"); }
                        }
                        else if (presetLow == "faceid")
                        {
                            if (isXl)
                            {
                                requireIPAdapterModel("ip-adapter-faceid_sdxl.bin", "https://huggingface.co/h94/IP-Adapter-FaceID/resolve/main/ip-adapter-faceid_sdxl.bin");
                                requireLora("ip-adapter-faceid_sdxl_lora.safetensors", "https://huggingface.co/h94/IP-Adapter-FaceID/resolve/main/ip-adapter-faceid_sdxl_lora.safetensors");
                            }
                            else
                            {
                                requireIPAdapterModel("ip-adapter-faceid_sd15.bin", "https://huggingface.co/h94/IP-Adapter-FaceID/resolve/main/ip-adapter-faceid_sd15.bin");
                                requireLora("ip-adapter-faceid_sd15_lora.safetensors", "https://huggingface.co/h94/IP-Adapter-FaceID/resolve/main/ip-adapter-faceid_sd15_lora.safetensors");
                            }
                        }
                        else if (presetLow.StartsWith("faceid plus -"))
                        {
                            if (isXl) { throw new InvalidOperationException("IP-Adapter FaceID plus model is not supported for SDXL"); }
                            else
                            {
                                requireIPAdapterModel("ip-adapter-faceid-plus_sd15.bin", "https://huggingface.co/h94/IP-Adapter-FaceID/resolve/main/ip-adapter-faceid-plus_sd15.bin");
                                requireLora("ip-adapter-faceid-plus_sd15_lora.safetensors", "https://huggingface.co/h94/IP-Adapter-FaceID/resolve/main/ip-adapter-faceid-plus_sd15_lora.safetensors");
                            }
                        }
                        else if (presetLow.StartsWith("faceid plus v2"))
                        {
                            if (isXl)
                            {
                                requireIPAdapterModel("ip-adapter-faceid-plusv2_sdxl.bin", "https://huggingface.co/h94/IP-Adapter-FaceID/resolve/main/ip-adapter-faceid-plusv2_sdxl.bin");
                                requireLora("ip-adapter-faceid-plusv2_sdxl_lora.safetensors", "https://huggingface.co/h94/IP-Adapter-FaceID/resolve/main/ip-adapter-faceid-plusv2_sdxl_lora.safetensors");
                            }
                            else
                            {
                                requireIPAdapterModel("ip-adapter-faceid-plusv2_sd15.bin", "https://huggingface.co/h94/IP-Adapter-FaceID/resolve/main/ip-adapter-faceid-plusv2_sd15.bin");
                                requireLora("ip-adapter-faceid-plusv2_sd15_lora.safetensors", "https://huggingface.co/h94/IP-Adapter-FaceID/resolve/main/ip-adapter-faceid-plusv2_sd15_lora.safetensors");
                            }
                        }
                        string ipAdapterLoader;
                        if (presetLow.StartsWith("faceid"))
                        {
                            ipAdapterLoader = g.CreateNode("IPAdapterUnifiedLoaderFaceID", new JObject()
                            {
                                ["model"] = g.FinalModel,
                                ["preset"] = ipAdapter,
                                ["lora_strength"] = 0.6,
                                ["provider"] = "CPU"
                            });
                        }
                        else
                        {
                            ipAdapterLoader = g.CreateNode("IPAdapterUnifiedLoader", new JObject()
                            {
                                ["model"] = g.FinalModel,
                                ["preset"] = ipAdapter
                            });
                        }
                        string ipAdapterNode = g.CreateNode("IPAdapter", new JObject()
                        {
                            ["model"] = new JArray() { ipAdapterLoader, 0 },
                            ["ipadapter"] = new JArray() { ipAdapterLoader, 1 },
                            ["image"] = new JArray() { lastImage, 0 },
                            ["weight"] = g.UserInput.Get(ComfyUIBackendExtension.IPAdapterWeight, 1),
                            ["start_at"] = 0.0,
                            ["end_at"] = 1.0,
                            ["weight_type"] = "standard" // TODO: ...???
                        });
                        g.FinalModel = [ipAdapterNode, 0];
                    }
                    else if (g.Features.Contains("cubiqipadapter"))
                    {
                        string ipAdapterLoader = g.CreateNode("IPAdapterModelLoader", new JObject()
                        {
                            ["ipadapter_file"] = ipAdapter
                        });
                        string ipAdapterNode = g.CreateNode("IPAdapterApply", new JObject()
                        {
                            ["ipadapter"] = new JArray() { ipAdapterLoader, 0 },
                            ["model"] = g.FinalModel,
                            ["image"] = new JArray() { lastImage, 0 },
                            ["clip_vision"] = new JArray() { $"{ipAdapterVisionLoader}", 0 },
                            ["weight"] = g.UserInput.Get(ComfyUIBackendExtension.IPAdapterWeight, 1),
                            ["noise"] = 0,
                            ["weight_type"] = "original" // TODO: ...???
                        });
                        g.FinalModel = [ipAdapterNode, 0];
                    }
                    else
                    {
                        string ipAdapterNode = g.CreateNode("IPAdapter", new JObject()
                        {
                            ["model"] = g.FinalModel,
                            ["image"] = new JArray() { lastImage, 0 },
                            ["clip_vision"] = new JArray() { $"{ipAdapterVisionLoader}", 0 },
                            ["weight"] = g.UserInput.Get(ComfyUIBackendExtension.IPAdapterWeight, 1),
                            ["model_name"] = ipAdapter,
                            ["dtype"] = "fp16" // TODO: ...???
                        });
                        g.FinalModel = [ipAdapterNode, 0];
                    }
                }
            }
        }, -7);
        #endregion
        #region Negative Prompt
        AddStep(g =>
        {
            g.FinalNegativePrompt = g.CreateConditioning(g.UserInput.Get(T2IParamTypes.NegativePrompt, ""), g.FinalClip, g.UserInput.Get(T2IParamTypes.Model), false, "7");
        }, -7);
        #endregion
        #region ControlNet
        AddStep(g =>
        {
            Image firstImage = g.UserInput.Get(T2IParamTypes.Controlnets[0].Image, null) ?? g.UserInput.Get(T2IParamTypes.InitImage, null);
            for (int i = 0; i < 3; i++)
            {
                T2IParamTypes.ControlNetParamHolder controlnetParams = T2IParamTypes.Controlnets[i];
                if (g.UserInput.TryGet(controlnetParams.Model, out T2IModel controlModel)
                    && g.UserInput.TryGet(controlnetParams.Strength, out double controlStrength))
                {
                    string imageInput = "${" + controlnetParams.Image.Type.ID + "}";
                    if (!g.UserInput.TryGet(controlnetParams.Image, out Image img))
                    {
                        if (firstImage is null)
                        {
                            Logs.Verbose($"Following error relates to parameters: {g.UserInput.ToJSON().ToDenseDebugString()}");
                            throw new InvalidDataException("Must specify either a ControlNet Image, or Init image. Or turn off ControlNet if not wanted.");
                        }
                        img = firstImage;
                    }
                    string imageNode = g.CreateLoadImageNode(img, imageInput, true);
                    if (!g.UserInput.TryGet(ComfyUIBackendExtension.ControlNetPreprocessorParams[i], out string preprocessor))
                    {
                        preprocessor = "none";
                        string wantedPreproc = controlModel.Metadata?.Preprocessor;
                        if (!string.IsNullOrWhiteSpace(wantedPreproc))
                        {
                            string[] procs = [.. ComfyUIBackendExtension.ControlNetPreprocessors.Keys];
                            bool getBestFor(string phrase)
                            {
                                string result = procs.FirstOrDefault(m => m.ToLowerFast().Contains(phrase.ToLowerFast()));
                                if (result is not null)
                                {
                                    preprocessor = result;
                                    return true;
                                }
                                return false;
                            }
                            if (wantedPreproc == "depth")
                            {
                                if (!getBestFor("midas-depthmap") && !getBestFor("depthmap") && !getBestFor("depth") && !getBestFor("midas") && !getBestFor("zoe") && !getBestFor("leres"))
                                {
                                    throw new InvalidDataException("No preprocessor found for depth - please install a Comfy extension that adds eg MiDaS depthmap preprocessors, or select 'none' if using a manual depthmap");
                                }
                            }
                            else if (wantedPreproc == "canny")
                            {
                                if (!getBestFor("cannyedge") && !getBestFor("canny"))
                                {
                                    preprocessor = "none";
                                }
                            }
                            else if (wantedPreproc == "sketch")
                            {
                                if (!getBestFor("sketch") && !getBestFor("lineart") && !getBestFor("scribble"))
                                {
                                    preprocessor = "none";
                                }
                            }
                        }
                    }
                    if (preprocessor.ToLowerFast() != "none")
                    {
                        JToken objectData = ComfyUIBackendExtension.ControlNetPreprocessors[preprocessor] ?? throw new InvalidDataException($"ComfyUI backend does not have a preprocessor named '{preprocessor}'");
                        string preProcNode = g.CreateNode(preprocessor, (_, n) =>
                        {
                            n["inputs"] = new JObject()
                            {
                                ["image"] = new JArray() { $"{imageNode}", 0 }
                            };
                            foreach ((string key, JToken data) in (JObject)objectData["input"]["required"])
                            {
                                if (key == "mask")
                                {
                                    if (g.FinalMask is null)
                                    {
                                        throw new InvalidOperationException($"ControlNet Preprocessor '{preprocessor}' requires a mask. Please set a mask under the Init Image parameter group.");
                                    }
                                    n["inputs"]["mask"] = g.FinalMask;
                                }
                                else if (data.Count() == 2 && data[1] is JObject settings && settings.TryGetValue("default", out JToken defaultValue))
                                {
                                    n["inputs"][key] = defaultValue;
                                }
                            }
                            if (((JObject)objectData["input"]).TryGetValue("optional", out JToken optional))
                            {
                                foreach ((string key, JToken data) in (JObject)optional)
                                {
                                    if (data.Count() == 2 && data[1] is JObject settings && settings.TryGetValue("default", out JToken defaultValue))
                                    {
                                        n["inputs"][key] = defaultValue;
                                    }
                                }
                            }
                        });
                        g.NodeHelpers["controlnet_preprocessor"] = $"{preProcNode}";
                        if (g.UserInput.Get(T2IParamTypes.ControlNetPreviewOnly))
                        {
                            g.FinalImageOut = [preProcNode, 0];
                            g.CreateImageSaveNode(g.FinalImageOut, "9");
                            g.SkipFurtherSteps = true;
                            return;
                        }
                        imageNode = preProcNode;
                    }
                    else if (g.UserInput.Get(T2IParamTypes.ControlNetPreviewOnly))
                    {
                        throw new InvalidDataException("Cannot preview a ControlNet preprocessor without any preprocessor enabled.");
                    }
                    // TODO: Preprocessor
                    string controlModelNode = g.CreateNode("ControlNetLoader", new JObject()
                    {
                        ["control_net_name"] = controlModel.ToString(g.ModelFolderFormat)
                    });
                    string applyNode = g.CreateNode("ControlNetApplyAdvanced", new JObject()
                    {
                        ["positive"] = g.FinalPrompt,
                        ["negative"] = g.FinalNegativePrompt,
                        ["control_net"] = new JArray() { $"{controlModelNode}", 0 },
                        ["image"] = new JArray() { $"{imageNode}", 0 },
                        ["strength"] = controlStrength,
                        ["start_percent"] = g.UserInput.Get(controlnetParams.Start, 0),
                        ["end_percent"] = g.UserInput.Get(controlnetParams.End, 1)
                    });
                    g.FinalPrompt = [applyNode, 0];
                    g.FinalNegativePrompt = [applyNode, 1];
                }
            }
        }, -6);
        #endregion
        #region Sampler
        AddStep(g =>
        {
            int steps = g.UserInput.Get(T2IParamTypes.Steps);
            int startStep = 0;
            int endStep = 10000;
            if (g.UserInput.TryGet(T2IParamTypes.InitImage, out Image _) && g.UserInput.TryGet(T2IParamTypes.InitImageCreativity, out double creativity))
            {
                startStep = (int)Math.Round(steps * (1 - creativity));
            }
            if (g.UserInput.TryGet(T2IParamTypes.RefinerMethod, out string method) && method == "StepSwap" && g.UserInput.TryGet(T2IParamTypes.RefinerControl, out double refinerControl))
            {
                endStep = (int)Math.Round(steps * (1 - refinerControl));
            }
            if (g.UserInput.TryGet(T2IParamTypes.EndStepsEarly, out double endEarly))
            {
                endStep = (int)(steps * (1 - endEarly));
            }
            double cfg = g.UserInput.Get(T2IParamTypes.CFGScale);
            if (steps == 0 || endStep <= startStep)
            {
                g.FinalSamples = g.FinalLatentImage;
            }
            else
            {
                g.CreateKSampler(g.FinalModel, g.FinalPrompt, g.FinalNegativePrompt, g.FinalLatentImage, cfg, steps, startStep, endStep,
                    g.UserInput.Get(T2IParamTypes.Seed), g.UserInput.Get(T2IParamTypes.RefinerMethod, "none") == "StepSwapNoisy", g.MainSamplerAddNoise, id: "10");
                if (g.UserInput.Get(T2IParamTypes.UseReferenceOnly, false))
                {
                    string fromBatch = g.CreateNode("LatentFromBatch", new JObject()
                    {
                        ["samples"] = new JArray() { "10", 0 },
                        ["batch_index"] = 1,
                        ["length"] = 1
                    });
                    g.FinalSamples = [fromBatch, 0];
                }
            }
        }, -5);
        JArray doMaskShrinkApply(WorkflowGenerator g, JArray imgIn)
        {
            (string boundsNode, string croppedMask, string masked) = g.MaskShrunkInfo;
            g.MaskShrunkInfo = (null, null, null);
            if (boundsNode is not null)
            {
                imgIn = g.RecompositeCropped(boundsNode, [croppedMask, 0], g.FinalInputImage, imgIn);
            }
            else if (g.UserInput.Get(T2IParamTypes.InitImageRecompositeMask, true) && g.FinalMask is not null && !g.NodeHelpers.ContainsKey("recomposite_mask_result"))
            {
                imgIn = g.CompositeMask(g.FinalInputImage, imgIn, g.FinalMask);
            }
            g.NodeHelpers["recomposite_mask_result"] = $"{imgIn[0]}";
            return imgIn;
        }
        #endregion
        #region Refiner
        AddStep(g =>
        {
            if (g.UserInput.TryGet(T2IParamTypes.RefinerMethod, out string method)
                && g.UserInput.TryGet(T2IParamTypes.RefinerControl, out double refinerControl)
                && g.UserInput.TryGet(ComfyUIBackendExtension.RefinerUpscaleMethod, out string upscaleMethod))
            {
                g.IsRefinerStage = true;
                JArray origVae = g.FinalVae, prompt = g.FinalPrompt, negPrompt = g.FinalNegativePrompt;
                bool modelMustReencode = false;
                if (g.UserInput.TryGet(T2IParamTypes.RefinerModel, out T2IModel refineModel) && refineModel is not null)
                {
                    T2IModel baseModel = g.UserInput.Get(T2IParamTypes.Model);
                    modelMustReencode = refineModel.ModelClass?.CompatClass != "stable-diffusion-xl-v1-refiner" || baseModel.ModelClass?.CompatClass != "stable-diffusion-xl-v1";
                    g.NoVAEOverride = refineModel.ModelClass?.CompatClass != baseModel.ModelClass?.CompatClass;
                    g.FinalLoadedModel = refineModel;
                    (g.FinalLoadedModel, g.FinalModel, g.FinalClip, g.FinalVae) = g.CreateStandardModelLoader(refineModel, "Refiner", "20");
                    prompt = g.CreateConditioning(g.UserInput.Get(T2IParamTypes.Prompt), g.FinalClip, refineModel, true);
                    negPrompt = g.CreateConditioning(g.UserInput.Get(T2IParamTypes.NegativePrompt), g.FinalClip, refineModel, false);
                    g.NoVAEOverride = false;
                }
                bool doSave = g.UserInput.Get(T2IParamTypes.SaveIntermediateImages, false);
                bool doUspcale = g.UserInput.TryGet(T2IParamTypes.RefinerUpscale, out double refineUpscale) && refineUpscale != 1;
                // TODO: Better same-VAE check
                bool doPixelUpscale = doUspcale && (upscaleMethod.StartsWith("pixel-") || upscaleMethod.StartsWith("model-"));
                if (modelMustReencode || doPixelUpscale || doSave || g.MaskShrunkInfo.Item1 is not null)
                {
                    g.CreateVAEDecode(origVae, g.FinalSamples, "24");
                    JArray pixelsNode = ["24", 0];
                    pixelsNode = doMaskShrinkApply(g, pixelsNode);
                    if (doSave)
                    {
                        g.CreateImageSaveNode(pixelsNode, "29");
                    }
                    if (doPixelUpscale)
                    {
                        if (upscaleMethod.StartsWith("pixel-"))
                        {
                            g.CreateNode("ImageScaleBy", new JObject()
                            {
                                ["image"] = pixelsNode,
                                ["upscale_method"] = upscaleMethod.After("pixel-"),
                                ["scale_by"] = refineUpscale
                            }, "26");
                        }
                        else
                        {
                            g.CreateNode("UpscaleModelLoader", new JObject()
                            {
                                ["model_name"] = upscaleMethod.After("model-")
                            }, "27");
                            g.CreateNode("ImageUpscaleWithModel", new JObject()
                            {
                                ["upscale_model"] = new JArray() { "27", 0 },
                                ["image"] = pixelsNode
                            }, "28");
                            g.CreateNode("ImageScale", new JObject()
                            {
                                ["image"] = new JArray() { "28", 0 },
                                ["width"] = (int)Math.Round(g.UserInput.Get(T2IParamTypes.Width) * refineUpscale),
                                ["height"] = (int)Math.Round(g.UserInput.GetImageHeight() * refineUpscale),
                                ["upscale_method"] = "bilinear",
                                ["crop"] = "disabled"
                            }, "26");
                        }
                        pixelsNode = ["26", 0];
                        if (refinerControl <= 0)
                        {
                            g.FinalImageOut = pixelsNode;
                            return;
                        }
                    }
                    if (modelMustReencode || doPixelUpscale)
                    {
                        g.CreateVAEEncode(g.FinalVae, pixelsNode, "25");
                        g.FinalSamples = ["25", 0];
                    }
                }
                if (doUspcale && upscaleMethod.StartsWith("latent-"))
                {
                    g.CreateNode("LatentUpscaleBy", new JObject()
                    {
                        ["samples"] = g.FinalSamples,
                        ["upscale_method"] = upscaleMethod.After("latent-"),
                        ["scale_by"] = refineUpscale
                    }, "26");
                    g.FinalSamples = ["26", 0];
                }
                JArray model = g.FinalModel;
                if (g.UserInput.TryGet(ComfyUIBackendExtension.RefinerHyperTile, out int tileSize))
                {
                    string hyperTileNode = g.CreateNode("HyperTile", new JObject()
                    {
                        ["model"] = model,
                        ["tile_size"] = tileSize,
                        ["swap_size"] = 2, // TODO: Do these other params matter?
                        ["max_depth"] = 0,
                        ["scale_depth"] = false
                    });
                    model = [hyperTileNode, 0];
                }
                int steps = g.UserInput.Get(T2IParamTypes.RefinerSteps, g.UserInput.Get(T2IParamTypes.Steps));
                double cfg = g.UserInput.Get(T2IParamTypes.CFGScale);
                g.CreateKSampler(model, prompt, negPrompt, g.FinalSamples, cfg, steps, (int)Math.Round(steps * (1 - refinerControl)), 10000,
                    g.UserInput.Get(T2IParamTypes.Seed) + 1, false, method != "StepSwapNoisy", id: "23", doTiled: g.UserInput.Get(T2IParamTypes.RefinerDoTiling, false));
                g.FinalSamples = ["23", 0];
                g.IsRefinerStage = false;
            }
        }, -4);
        #endregion
        #region VAEDecode
        AddStep(g =>
        {
            g.CreateVAEDecode(g.FinalVae, g.FinalSamples, "8");
            g.FinalImageOut = doMaskShrinkApply(g, ["8", 0]);
        }, 1);
        #endregion
        #region Segmentation Processing
        AddStep(g =>
        {
            PromptRegion.Part[] parts = new PromptRegion(g.UserInput.Get(T2IParamTypes.Prompt, "")).Parts.Where(p => p.Type == PromptRegion.PartType.Segment).ToArray();
            if (parts.Any())
            {
                if (g.UserInput.Get(T2IParamTypes.SaveIntermediateImages, false))
                {
                    g.CreateImageSaveNode(g.FinalImageOut, g.GetStableDynamicID(50000, 0));
                }
                T2IModel t2iModel = g.FinalLoadedModel;
                JArray model = g.FinalModel, clip = g.FinalClip, vae = g.FinalVae;
                if (g.UserInput.TryGet(T2IParamTypes.SegmentModel, out T2IModel segmentModel))
                {
                    if (segmentModel.ModelClass?.CompatClass != t2iModel.ModelClass?.CompatClass)
                    {
                        g.NoVAEOverride = true;
                    }
                    t2iModel = segmentModel;
                    (t2iModel, model, clip, vae) = g.CreateStandardModelLoader(t2iModel, "Refiner");
                }
                PromptRegion negativeRegion = new(g.UserInput.Get(T2IParamTypes.NegativePrompt, ""));
                PromptRegion.Part[] negativeParts = negativeRegion.Parts.Where(p => p.Type == PromptRegion.PartType.Segment).ToArray();
                for (int i = 0; i < parts.Length; i++)
                {
                    PromptRegion.Part part = parts[i];
                    string segmentNode;
                    if (part.DataText.StartsWith("yolo-"))
                    {
                        string fullname = part.DataText.After('-');
                        (string mname, string indexText) = fullname.BeforeAndAfterLast('-');
                        if (!string.IsNullOrWhiteSpace(indexText) && int.TryParse(indexText, out int index))
                        {
                            fullname = mname;
                        }
                        else
                        {
                            index = 0;
                        }
                        segmentNode = g.CreateNode("SwarmYoloDetection", new JObject()
                        {
                            ["image"] = g.FinalImageOut,
                            ["model_name"] = fullname,
                            ["index"] = index
                        });
                    }
                    else
                    {
                        segmentNode = g.CreateNode("SwarmClipSeg", new JObject()
                        {
                            ["images"] = g.FinalImageOut,
                            ["match_text"] = part.DataText,
                            ["threshold"] = part.Strength
                        });
                    }
                    int blurAmt = g.UserInput.Get(T2IParamTypes.SegmentMaskBlur, 10);
                    if (blurAmt > 0)
                    {
                        segmentNode = g.CreateNode("SwarmMaskBlur", new JObject()
                        {
                            ["mask"] = new JArray() { segmentNode, 0 },
                            ["blur_radius"] = blurAmt,
                            ["sigma"] = 1
                        });
                    }
                    int growAmt = g.UserInput.Get(T2IParamTypes.SegmentMaskGrow, 16);
                    if (growAmt > 0)
                    {
                        segmentNode = g.CreateNode("GrowMask", new JObject()
                        {
                            ["mask"] = new JArray() { segmentNode, 0 },
                            ["expand"] = growAmt,
                            ["tapered_corners"] = true
                        });
                    }
                    if (g.UserInput.Get(T2IParamTypes.SaveSegmentMask, false))
                    {
                        string imageNode = g.CreateNode("MaskToImage", new JObject()
                        {
                            ["mask"] = new JArray() { segmentNode, 0 }
                        });
                        g.CreateImageSaveNode([imageNode, 0], g.GetStableDynamicID(50000, 0));
                    }
                    (string boundsNode, string croppedMask, string masked) = g.CreateImageMaskCrop([segmentNode, 0], g.FinalImageOut, 8, vae, thresholdMax: g.UserInput.Get(T2IParamTypes.SegmentThresholdMax, 1));
                    g.EnableDifferential();
                    (model, clip) = g.LoadLorasForConfinement(part.ContextID, model, clip);
                    JArray prompt = g.CreateConditioning(part.Prompt, clip, t2iModel, true);
                    string neg = negativeParts.FirstOrDefault(p => p.DataText == part.DataText)?.Prompt ?? negativeRegion.GlobalPrompt;
                    JArray negPrompt = g.CreateConditioning(neg, clip, t2iModel, false);
                    int steps = g.UserInput.Get(T2IParamTypes.Steps);
                    int startStep = (int)Math.Round(steps * (1 - part.Strength2));
                    long seed = g.UserInput.Get(T2IParamTypes.Seed) + 2 + i;
                    double cfg = g.UserInput.Get(T2IParamTypes.CFGScale);
                    string sampler = g.CreateKSampler(model, prompt, negPrompt, [masked, 0], cfg, steps, startStep, 10000, seed, false, true);
                    string decoded = g.CreateVAEDecode(vae, [sampler, 0]);
                    g.FinalImageOut = g.RecompositeCropped(boundsNode, [croppedMask, 0], g.FinalImageOut, [decoded, 0]);
                }
            }
        }, 5);
        #endregion
        #region SaveImage
        AddStep(g =>
        {
            PromptRegion.Part[] parts = new PromptRegion(g.UserInput.Get(T2IParamTypes.Prompt, "")).Parts.Where(p => p.Type == PromptRegion.PartType.ClearSegment).ToArray();
            foreach (PromptRegion.Part part in parts)
            {
                string segmentNode = g.CreateNode("SwarmClipSeg", new JObject()
                {
                    ["images"] = g.FinalImageOut,
                    ["match_text"] = part.DataText,
                    ["threshold"] = part.Strength
                });
                string blurNode = g.CreateNode("SwarmMaskBlur", new JObject()
                {
                    ["mask"] = new JArray() { segmentNode, 0 },
                    ["blur_radius"] = 10,
                    ["sigma"] = 1
                });
                string thresholded = g.CreateNode("SwarmMaskThreshold", new JObject()
                {
                    ["mask"] = new JArray() { blurNode, 0 },
                    ["min"] = 0.2,
                    ["max"] = 0.6
                });
                string joined = g.CreateNode("JoinImageWithAlpha", new JObject()
                {
                    ["image"] = g.FinalImageOut,
                    ["alpha"] = new JArray() { thresholded, 0 }
                });
                g.FinalImageOut = [joined, 0];
            }
            if (g.UserInput.Get(T2IParamTypes.RemoveBackground, false))
            {
                string removed = g.CreateNode("SwarmRemBg", new JObject()
                {
                    ["images"] = g.FinalImageOut
                });
                g.FinalImageOut = [removed, 0];
            }
            g.CreateImageSaveNode(g.FinalImageOut, "9");
        }, 10);
        #endregion
        #region Video
        AddStep(g =>
        {
            if (g.UserInput.TryGet(T2IParamTypes.VideoModel, out T2IModel vidModel))
            {
                string loader = g.CreateNode("ImageOnlyCheckpointLoader", new JObject()
                {
                    ["ckpt_name"] = vidModel.ToString()
                });
                JArray model = [loader, 0];
                JArray clipVision = [loader, 1];
                JArray vae = [loader, 2];
                double minCfg = g.UserInput.Get(T2IParamTypes.VideoMinCFG, 1);
                if (minCfg >= 0)
                {
                    string cfgGuided = g.CreateNode("VideoLinearCFGGuidance", new JObject()
                    {
                        ["model"] = model,
                        ["min_cfg"] = minCfg
                    });
                    model = [cfgGuided, 0];
                }
                int frames = g.UserInput.Get(T2IParamTypes.VideoFrames, 25);
                int fps = g.UserInput.Get(T2IParamTypes.VideoFPS, 6);
                string resFormat = g.UserInput.Get(T2IParamTypes.VideoResolution, "Model Preferred");
                int width = vidModel.StandardWidth <= 0 ? 1024 : vidModel.StandardWidth;
                int height = vidModel.StandardHeight <= 0 ? 576 : vidModel.StandardHeight;
                int imageWidth = g.UserInput.Get(T2IParamTypes.Width, width);
                int imageHeight = g.UserInput.GetImageHeight();
                if (resFormat == "Image Aspect, Model Res")
                {
                    if (width == 1024 && height == 576 && imageWidth == 1344 && imageHeight == 768)
                    {
                        width = 1024;
                        height = 576;
                    }
                    else
                    {
                        (width, height) = Utilities.ResToModelFit(imageWidth, imageHeight, width * height);
                    }
                }
                else if (resFormat == "Image")
                {
                    width = imageWidth;
                    height = imageHeight;
                }
                string conditioning = g.CreateNode("SVD_img2vid_Conditioning", new JObject()
                {
                    ["clip_vision"] = clipVision,
                    ["init_image"] = g.FinalImageOut,
                    ["vae"] = vae,
                    ["width"] = width,
                    ["height"] = height,
                    ["video_frames"] = frames,
                    ["motion_bucket_id"] = g.UserInput.Get(T2IParamTypes.VideoMotionBucket, 127),
                    ["fps"] = fps,
                    ["augmentation_level"] = g.UserInput.Get(T2IParamTypes.VideoAugmentationLevel, 0)
                });
                JArray posCond = [conditioning, 0];
                JArray negCond = [conditioning, 1];
                JArray latent = [conditioning, 2];
                int steps = g.UserInput.Get(T2IParamTypes.VideoSteps, 20);
                double cfg = g.UserInput.Get(T2IParamTypes.VideoCFG, 2.5);
                string previewType = g.UserInput.Get(ComfyUIBackendExtension.VideoPreviewType, "animate");
                string samplered = g.CreateKSampler(model, posCond, negCond, latent, cfg, steps, 0, 10000, g.UserInput.Get(T2IParamTypes.Seed) + 42, false, true, sigmin: 0.002, sigmax: 1000, previews: previewType, defsampler: "dpmpp_2m_sde_gpu", defscheduler: "karras");
                g.FinalLatentImage = [samplered, 0];
                string decoded = g.CreateVAEDecode(vae, g.FinalLatentImage);
                g.FinalImageOut = [decoded, 0];
                string format = g.UserInput.Get(T2IParamTypes.VideoFormat, "webp").ToLowerFast();
                if (g.UserInput.TryGet(ComfyUIBackendExtension.VideoFrameInterpolationMethod, out string method) && g.UserInput.TryGet(ComfyUIBackendExtension.VideoFrameInterpolationMultiplier, out int mult) && mult > 1)
                {
                    if (g.UserInput.Get(T2IParamTypes.SaveIntermediateImages, false))
                    {
                        g.CreateNode("SwarmSaveAnimationWS", new JObject()
                        {
                            ["images"] = g.FinalImageOut,
                            ["fps"] = fps,
                            ["lossless"] = false,
                            ["quality"] = 95,
                            ["method"] = "default",
                            ["format"] = format
                        }, g.GetStableDynamicID(50000, 0));
                    }
                    if (method == "RIFE")
                    {
                        string rife = g.CreateNode("RIFE VFI", new JObject()
                        {
                            ["frames"] = g.FinalImageOut,
                            ["multiplier"] = mult,
                            ["ckpt_name"] = "rife47.pth",
                            ["clear_cache_after_n_frames"] = 10,
                            ["fast_mode"] = true,
                            ["ensemble"] = true,
                            ["scale_factor"] = 1
                        });
                        g.FinalImageOut = [rife, 0];
                    }
                    else if (method == "FILM")
                    {
                        string film = g.CreateNode("FILM VFI", new JObject()
                        {
                            ["frames"] = g.FinalImageOut,
                            ["multiplier"] = mult,
                            ["ckpt_name"] = "film_net_fp32.pt",
                            ["clear_cache_after_n_frames"] = 10
                        });
                        g.FinalImageOut = [film, 0];
                    }
                    fps *= mult;
                }
                if (g.UserInput.Get(T2IParamTypes.VideoBoomerang, false))
                {
                    string bounced = g.CreateNode("SwarmVideoBoomerang", new JObject()
                    {
                        ["images"] = g.FinalImageOut
                    });
                    g.FinalImageOut = [bounced, 0];
                }
                g.CreateNode("SwarmSaveAnimationWS", new JObject()
                {
                    ["images"] = g.FinalImageOut,
                    ["fps"] = fps,
                    ["lossless"] = false,
                    ["quality"] = 95,
                    ["method"] = "default",
                    ["format"] = format
                });
            }
        }, 11);
        #endregion
    }

    /// <summary>The raw user input data.</summary>
    public T2IParamInput UserInput;

    /// <summary>The output workflow object.</summary>
    public JObject Workflow;

    /// <summary>Lastmost node ID for key input trackers.</summary>
    public JArray FinalModel = ["4", 0],
        FinalClip = ["4", 1],
        FinalInputImage = null,
        FinalMask = null,
        FinalVae = ["4", 2],
        FinalLatentImage = ["5", 0],
        FinalPrompt = ["6", 0],
        FinalNegativePrompt = ["7", 0],
        FinalSamples = ["10", 0],
        FinalImageOut = ["8", 0],
        LoadingModel = null, LoadingClip = null, LoadingVAE = null;

    /// <summary>If true, the init image was altered in latent space and is no longer valid.</summary>
    public bool InitialImageIsAlteredAsLatent = false;

    /// <summary>If true, something has required the workflow stop now.</summary>
    public bool SkipFurtherSteps = false;

    /// <summary>What model currently matches <see cref="FinalModel"/>.</summary>
    public T2IModel FinalLoadedModel;

    /// <summary>Mapping of any extra nodes to keep track of, Name->ID, eg "MyNode" -> "15".</summary>
    public Dictionary<string, string> NodeHelpers = [];

    /// <summary>Last used ID, tracked to safely add new nodes with sequential IDs. Note that this starts at 100, as below 100 is reserved for constant node IDs.</summary>
    public int LastID = 100;

    /// <summary>Model folder separator format, if known.</summary>
    public string ModelFolderFormat;

    /// <summary>Type id ('Base', 'Refiner') of the current loading model.</summary>
    public string LoadingModelType;

    /// <summary>If true, user-selected VAE may be wrong, so ignore it.</summary>
    public bool NoVAEOverride = false;

    /// <summary>If true, the generator is currently working on the refiner stage.</summary>
    public bool IsRefinerStage = false;

    /// <summary>If true, the main sampler should add noise. If false, it shouldn't.</summary>
    public bool MainSamplerAddNoise = true;

    /// <summary>If true, Differential Diffusion node has been attached to the current model.</summary>
    public bool IsDifferentialDiffusion = false;

    /// <summary>Outputs of <see cref="CreateImageMaskCrop(JArray, JArray, int)"/> if used for the main image.</summary>
    public (string, string, string) MaskShrunkInfo = (null, null, null);

    /// <summary>Gets the current loaded model class.</summary>
    public T2IModelClass CurrentModelClass()
    {
        FinalLoadedModel ??= UserInput.Get(T2IParamTypes.Model, null);
        return FinalLoadedModel?.ModelClass;
    }

    /// <summary>Gets the current loaded model compat class.</summary>
    public string CurrentCompatClass()
    {
        return CurrentModelClass()?.CompatClass;
    }

    /// <summary>Returns true if the current model is Stable Cascade.</summary>
    public bool IsCascade()
    {
        string clazz = CurrentCompatClass();
        return clazz is not null && clazz == "stable-cascade-v1";
    }

    /// <summary>Returns true if the current model is Stable Diffusion 3.</summary>
    public bool IsSD3()
    {
        string clazz = CurrentCompatClass();
        return clazz is not null && clazz == "stable-diffusion-v3-medium";
    }

    /// <summary>Gets a dynamic ID within a semi-stable registration set.</summary>
    public string GetStableDynamicID(int index, int offset)
    {
        int id = 1000 + index + offset;
        string result = $"{id}";
        if (HasNode(result))
        {
            return GetStableDynamicID(index, offset + 1);
        }
        return result;
    }

    /// <summary>Creates a new node with the given class type and configuration action, and optional manual ID.</summary>
    public string CreateNode(string classType, Action<string, JObject> configure, string id = null)
    {
        id ??= $"{LastID++}";
        JObject obj = new() { ["class_type"] = classType };
        configure(id, obj);
        Workflow[id] = obj;
        return id;
    }

    /// <summary>Creates a new node with the given class type and input data, and optional manual ID.</summary>
    public string CreateNode(string classType, JObject input, string id = null)
    {
        return CreateNode(classType, (_, n) => n["inputs"] = input, id);
    }

    /// <summary>Helper to download a core model file required by the workflow.</summary>
    public void DownloadModel(string name, string filePath, string url)
    {
        if (File.Exists(filePath))
        {
            return;
        }
        lock (ModelDownloaderLock)
        {
            if (File.Exists(filePath)) // Double-check in case another thread downloaded it
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            Logs.Info($"Downloading {name} to {filePath}...");
            double nextPerc = 0.05;
            try
            {
                Utilities.DownloadFile(url, filePath, (bytes, total) =>
                {
                    double perc = bytes / (double)total;
                    if (perc >= nextPerc)
                    {
                        Logs.Info($"{name} download at {perc * 100:0.0}%...");
                        // TODO: Send a signal back so a progress bar can be displayed on a UI
                        nextPerc = Math.Round(perc / 0.05) * 0.05 + 0.05;
                    }
                }).Wait();
            }
            catch (Exception ex)
            {
                Logs.Error($"Failed to download {name} from {url}: {ex.Message}");
                File.Delete(filePath);
                throw new InvalidOperationException("Required model download failed.");
            }
            Logs.Info($"Downloading complete, continuing.");
        }
    }

    /// <summary>Loads and applies LoRAs in the user parameters for the given LoRA confinement ID.</summary>
    public (JArray, JArray) LoadLorasForConfinement(int confinement, JArray model, JArray clip)
    {
        if (confinement < 0 || !UserInput.TryGet(T2IParamTypes.Loras, out List<string> loras))
        {
            return (model, clip);
        }
        List<string> weights = UserInput.Get(T2IParamTypes.LoraWeights);
        List<string> confinements = UserInput.Get(T2IParamTypes.LoraSectionConfinement);
        if (confinement > 0 && (confinements is null || confinements.Count == 0))
        {
            return (model, clip);
        }
        T2IModelHandler loraHandler = Program.T2IModelSets["LoRA"];
        for (int i = 0; i < loras.Count; i++)
        {
            if (!loraHandler.Models.TryGetValue(loras[i] + ".safetensors", out T2IModel lora))
            {
                if (!loraHandler.Models.TryGetValue(loras[i], out lora))
                {
                    throw new InvalidDataException($"LoRA Model '{loras[i]}' not found in the model set.");
                }
            }
            if (confinements is not null && confinements.Count > i)
            {
                int confinementId = int.Parse(confinements[i]);
                if (confinementId != confinement)
                {
                    continue;
                }
            }
            float weight = weights == null ? 1 : float.Parse(weights[i]);
            string newId = CreateNode("LoraLoader", new JObject()
            {
                ["model"] = model,
                ["clip"] = clip,
                ["lora_name"] = lora.ToString(ModelFolderFormat),
                ["strength_model"] = weight,
                ["strength_clip"] = weight
            }, GetStableDynamicID(500, i));
            model = [newId, 0];
            clip = [newId, 1];
        }
        return (model, clip);
    }

    /// <summary>Creates a new node to load an image.</summary>
    public string CreateLoadImageNode(Image img, string param, bool resize, string nodeId = null)
    {
        if (nodeId is null && NodeHelpers.TryGetValue($"imgloader_{param}_{resize}", out string alreadyLoaded))
        {
            return alreadyLoaded;
        }
        string result;
        if (Features.Contains("comfy_loadimage_b64") && !RestrictCustomNodes)
        {
            result = CreateNode("SwarmLoadImageB64", new JObject()
            {
                ["image_base64"] = (resize ? img.Resize(UserInput.Get(T2IParamTypes.Width), UserInput.GetImageHeight()) : img).AsBase64
            }, nodeId);
        }
        else
        {
            result = CreateNode("LoadImage", new JObject()
            {
                ["image"] = param
            }, nodeId);
        }
        NodeHelpers[$"imgloader_{param}_{resize}"] = result;
        return result;
    }

    /// <summary>Creates an automatic image mask-crop before sampling, to be followed by <see cref="RecompositeCropped(string, string, JArray, JArray)"/> after sampling.</summary>
    /// <param name="mask">The mask node input.</param>
    /// <param name="image">The image node input.</param>
    /// <param name="growBy">Number of pixels to grow the boundary by.</param>
    /// <param name="vae">The relevant VAE.</param>
    /// <param name="threshold">Optional minimum value threshold.</param>
    /// <returns>(boundsNode, croppedMask, maskedLatent).</returns>
    public (string, string, string) CreateImageMaskCrop(JArray mask, JArray image, int growBy, JArray vae, double threshold = 0.01, double thresholdMax = 1)
    {
        if (threshold > 0)
        {
            string thresholded = CreateNode("SwarmMaskThreshold", new JObject()
            {
                ["mask"] = mask,
                ["min"] = threshold,
                ["max"] = thresholdMax
            });
            mask = [thresholded, 0];
        }
        string boundsNode = CreateNode("SwarmMaskBounds", new JObject()
        {
            ["mask"] = mask,
            ["grow"] = growBy
        });
        string croppedImage = CreateNode("SwarmImageCrop", new JObject()
        {
            ["image"] = image,
            ["x"] = new JArray() { boundsNode, 0 },
            ["y"] = new JArray() { boundsNode, 1 },
            ["width"] = new JArray() { boundsNode, 2 },
            ["height"] = new JArray() { boundsNode, 3 }
        });
        string croppedMask = CreateNode("CropMask", new JObject()
        {
            ["mask"] = mask,
            ["x"] = new JArray() { boundsNode, 0 },
            ["y"] = new JArray() { boundsNode, 1 },
            ["width"] = new JArray() { boundsNode, 2 },
            ["height"] = new JArray() { boundsNode, 3 }
        });
        string scaledImage = CreateNode("SwarmImageScaleForMP", new JObject()
        {
            ["image"] = new JArray() { croppedImage, 0 },
            ["width"] = UserInput.Get(T2IParamTypes.Width, 1024),
            ["height"] = UserInput.GetImageHeight(),
            ["can_shrink"] = false
        });
        string vaeEncoded = CreateVAEEncode(vae, [scaledImage, 0], null, true);
        string masked = CreateNode("SetLatentNoiseMask", new JObject()
        {
            ["samples"] = new JArray() { vaeEncoded, 0 },
            ["mask"] = new JArray() { croppedMask, 0 }
        });
        return (boundsNode, croppedMask, masked);
    }

    /// <summary>Returns a masked image composite with mask thresholding.</summary>
    public JArray CompositeMask(JArray baseImage, JArray newImage, JArray mask)
    {
        string thresholded = CreateNode("ThresholdMask", new JObject()
        {
            ["mask"] = mask,
            ["value"] = 0.001
        });
        string composited = CreateNode("ImageCompositeMasked", new JObject()
        {
            ["destination"] = baseImage,
            ["source"] = newImage,
            ["mask"] = new JArray() { thresholded, 0 },
            ["x"] = 0,
            ["y"] = 0,
            ["resize_source"] = false
        });
        return [composited, 0];
    }

    /// <summary>Recomposites a masked image edit, after <see cref="CreateImageMaskCrop(JArray, JArray, int)"/> was used.</summary>
    public JArray RecompositeCropped(string boundsNode, JArray croppedMask, JArray firstImage, JArray newImage)
    {
        string scaledBack = CreateNode("ImageScale", new JObject()
        {
            ["image"] = newImage,
            ["width"] = new JArray() { boundsNode, 2 },
            ["height"] = new JArray() { boundsNode, 3 },
            ["upscale_method"] = "bilinear",
            ["crop"] = "disabled"
        });
        string thresholded = CreateNode("ThresholdMask", new JObject()
        {
            ["mask"] = croppedMask,
            ["value"] = 0.001
        });
        string composited = CreateNode("ImageCompositeMasked", new JObject()
        {
            ["destination"] = firstImage,
            ["source"] = new JArray() { scaledBack, 0 },
            ["mask"] = new JArray() { thresholded, 0 },
            ["x"] = new JArray() { boundsNode, 0 },
            ["y"] = new JArray() { boundsNode, 1 },
            ["resize_source"] = false
        });
        return [composited, 0];
    }

    /// <summary>Call to run the generation process and get the result.</summary>
    public JObject Generate()
    {
        Workflow = [];
        foreach (WorkflowGenStep step in Steps)
        {
            step.Action(this);
            if (SkipFurtherSteps)
            {
                break;
            }
        }
        return Workflow;
    }

    /// <summary>Returns true if the given node ID has already been used.</summary>
    public bool HasNode(string id)
    {
        return Workflow.ContainsKey(id);
    }

    /// <summary>Creates a node to save an image output.</summary>
    public string CreateImageSaveNode(JArray image, string id = null)
    {
        if (Features.Contains("comfy_saveimage_ws") && !RestrictCustomNodes)
        {
            return CreateNode("SwarmSaveImageWS", new JObject()
            {
                ["images"] = image
            }, id);
        }
        else
        {
            return CreateNode("SaveImage", new JObject()
            {
                ["filename_prefix"] = $"StableSwarmUI_{Random.Shared.Next():X4}_",
                ["images"] = image
            }, id);
        }
    }

    /// <summary>Creates a model loader and adapts it with any registered model adapters, and returns (Model, Clip, VAE).</summary>
    public (T2IModel, JArray, JArray, JArray) CreateStandardModelLoader(T2IModel model, string type, string id = null, bool noCascadeFix = false)
    {
        IsDifferentialDiffusion = false;
        LoadingModelType = type;
        if (!noCascadeFix && model.ModelClass?.ID == "stable-cascade-v1-stage-b" && model.Name.Contains("stage_b") && Program.MainSDModels.Models.TryGetValue(model.Name.Replace("stage_b", "stage_c"), out T2IModel altCascadeModel))
        {
            model = altCascadeModel;
        }
        if (model.ModelClass?.ID.EndsWith("/tensorrt") ?? false)
        {
            string baseArch = model.ModelClass?.ID?.Before('/');
            string trtType = ComfyUIWebAPI.ArchitecturesTRTCompat[baseArch];
            string trtloader = CreateNode("TensorRTLoader", new JObject()
            {
                ["unet_name"] = model.ToString(ModelFolderFormat),
                ["model_type"] = trtType
            }, id);
            LoadingModel = [trtloader, 0];
            // TODO: This is a hack
            T2IModel[] sameArch = [.. Program.MainSDModels.Models.Values.Where(m => m.ModelClass?.ID == baseArch)];
            if (sameArch.Length == 0)
            {
                throw new InvalidDataException($"No models found with architecture {baseArch}, cannot load CLIP/VAE for this Arch");
            }
            T2IModel matchedName = sameArch.FirstOrDefault(m => m.Name.Before('.') == model.Name.Before('.'));
            matchedName ??= sameArch.First();
            string secondaryNode = CreateNode("CheckpointLoaderSimple", new JObject()
            {
                ["ckpt_name"] = matchedName.ToString(ModelFolderFormat)
            });
            LoadingClip = [secondaryNode, 1];
            LoadingVAE = [secondaryNode, 2];
        }
        else if (model.Name.EndsWith(".engine"))
        {
            throw new InvalidDataException($"Model {model.Name} appears to be TensorRT lacks metadata to identify its architecture, cannot load");
        }
        else
        {
            string modelNode = CreateNode("CheckpointLoaderSimple", new JObject()
            {
                ["ckpt_name"] = model.ToString(ModelFolderFormat)
            }, id);
            LoadingModel = [modelNode, 0];
            LoadingClip = [modelNode, 1];
            LoadingVAE = [modelNode, 2];
        }
        string predType = model.Metadata?.PredictionType;
        if (CurrentCompatClass() == "stable-diffusion-v3-medium")
        {
            string sd3Node = CreateNode("ModelSamplingSD3", new JObject()
            {
                ["model"] = LoadingModel,
                ["shift"] = UserInput.Get(T2IParamTypes.SigmaShift, 3)
            });
            LoadingModel = [sd3Node, 0];
            void requireClipModel(string name, string url)
            {
                if (ClipModelsValid.Contains(name))
                {
                    return;
                }
                string filePath = Utilities.CombinePathWithAbsolute(Program.ServerSettings.Paths.ModelRoot, "clip", name);
                DownloadModel(name, filePath, url);
                ClipModelsValid.Add(name);
            }
            string tencs = model.Metadata?.TextEncoders ?? "";
            if (!UserInput.TryGet(T2IParamTypes.SD3TextEncs, out string mode))
            {
                if (tencs == "")
                {
                    mode = "CLIP Only";
                }
                else
                {
                    mode = null;
                }
            }
            if (mode == "CLIP Only" && tencs.Contains("clip_l") && !tencs.Contains("t5xxl")) { mode = null; }
            if (mode == "T5 Only" && !tencs.Contains("clip_l") && tencs.Contains("t5xxl")) { mode = null; }
            if (mode == "CLIP + T5" && tencs.Contains("clip_l") && tencs.Contains("t5xxl")) { mode = null; }
            if (mode is not null)
            {
                requireClipModel("clip_g_sdxl_base.safetensors", "https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/text_encoder_2/model.fp16.safetensors");
                requireClipModel("clip_l_sdxl_base.safetensors", "https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/text_encoder/model.fp16.safetensors");
                if (mode.Contains("T5"))
                {
                    requireClipModel("t5xxl_enconly.safetensors", "https://huggingface.co/mcmonkey/google_t5-v1_1-xxl_encoderonly/resolve/main/t5xxl_fp8_e4m3fn.safetensors");
                }
                if (mode == "T5 Only")
                {
                    string singleClipLoader = CreateNode("CLIPLoader", new JObject()
                    {
                        ["clip_name"] = "t5xxl_enconly.safetensors",
                        ["type"] = "sd3"
                    });
                    LoadingClip = [singleClipLoader, 0];
                }
                else if (mode == "CLIP Only")
                {
                    string dualClipLoader = CreateNode("DualCLIPLoader", new JObject()
                    {
                        ["clip_name1"] = "clip_g_sdxl_base.safetensors",
                        ["clip_name2"] = "clip_l_sdxl_base.safetensors",
                        ["type"] = "sd3"
                    });
                    LoadingClip = [dualClipLoader, 0];
                }
                else
                {
                    string tripleClipLoader = CreateNode("TripleCLIPLoader", new JObject()
                    {
                        ["clip_name1"] = "clip_g_sdxl_base.safetensors",
                        ["clip_name2"] = "clip_l_sdxl_base.safetensors",
                        ["clip_name3"] = "t5xxl_enconly.safetensors"
                    });
                    LoadingClip = [tripleClipLoader, 0];
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(predType))
        {
            string discreteNode = CreateNode("ModelSamplingDiscrete", new JObject()
            {
                ["model"] = LoadingModel,
                ["sampling"] = predType switch { "v" => "v_prediction", "v-zsnr" => "v_prediction", "epsilon" => "eps", _ => predType },
                ["zsnr"] = predType.Contains("zsnr")
            });
            LoadingModel = [discreteNode, 0];
        }
        foreach (WorkflowGenStep step in ModelGenSteps)
        {
            step.Action(this);
        }
        return (model, LoadingModel, LoadingClip, LoadingVAE);
    }

    /// <summary>Creates a VAEDecode node and returns its node ID.</summary>
    public string CreateVAEDecode(JArray vae, JArray latent, string id = null)
    {
        if (UserInput.TryGet(T2IParamTypes.VAETileSize, out int tileSize))
        {
            return CreateNode("VAEDecodeTiled", new JObject()
            {
                ["vae"] = vae,
                ["samples"] = latent,
                ["tile_size"] = tileSize
            }, id);
        }
        return CreateNode("VAEDecode", new JObject()
        {
            ["vae"] = vae,
            ["samples"] = latent
        }, id);
    }

    /// <summary>Default sampler type.</summary>
    public string DefaultSampler = "euler";

    /// <summary>Default sampler scheduler type.</summary>
    public string DefaultScheduler = "normal";

    /// <summary>Default previews type.</summary>
    public string DefaultPreviews = "default";

    /// <summary>Creates a KSampler and returns its node ID.</summary>
    public string CreateKSampler(JArray model, JArray pos, JArray neg, JArray latent, double cfg, int steps, int startStep, int endStep, long seed, bool returnWithLeftoverNoise, bool addNoise, double sigmin = -1, double sigmax = -1, string previews = null, string defsampler = null, string defscheduler = null, string id = null, bool rawSampler = false, bool doTiled = false)
    {
        bool willCascadeFix = false;
        JArray cascadeModel = null;
        if (!rawSampler && IsCascade() && FinalLoadedModel.Name.Contains("stage_c") && Program.MainSDModels.Models.TryGetValue(FinalLoadedModel.Name.Replace("stage_c", "stage_b"), out T2IModel bModel))
        {
            (_, cascadeModel, _, FinalVae) = CreateStandardModelLoader(bModel, LoadingModelType, null, true);
            willCascadeFix = true;
            defsampler ??= "euler_ancestral";
            defscheduler ??= "simple";
        }
        if (FinalLoadedModel?.ModelClass ?.ID == "stable-diffusion-xl-v1-edit")
        {
            // TODO: SamplerCustomAdvanced logic should be used for *all* models, not just ip2p
            if (FinalInputImage is null)
            {
                // TODO: Get the correct image (eg if edit is used as a refiner or something silly it should still work)
                string decoded = CreateVAEDecode(FinalVae, latent);
                FinalInputImage = [decoded, 0];
            }
            string ip2p2condNode = CreateNode("InstructPixToPixConditioning", new JObject()
            {
                ["positive"] = pos,
                ["negative"] = neg,
                ["vae"] = FinalVae,
                ["pixels"] = FinalInputImage
            });
            string noiseNode = CreateNode("RandomNoise", new JObject()
            {
                ["noise_seed"] = seed
            });
            // TODO: VarSeed, batching, etc. seed logic
            string cfgGuiderNode = CreateNode("DualCFGGuider", new JObject()
            {
                ["model"] = model,
                ["cond1"] = new JArray() { ip2p2condNode, 0 },
                ["cond2"] = new JArray() { ip2p2condNode, 1 },
                ["negative"] = neg,
                ["cfg_conds"] = cfg,
                ["cfg_cond2_negative"] = UserInput.Get(T2IParamTypes.IP2PCFG2, 1.5)
            });
            string samplerNode = CreateNode("KSamplerSelect", new JObject()
            {
                ["sampler_name"] = UserInput.Get(ComfyUIBackendExtension.SamplerParam, defsampler ?? DefaultSampler)
            });
            string scheduler = UserInput.Get(ComfyUIBackendExtension.SchedulerParam, defscheduler ?? DefaultScheduler).ToLowerFast();
            double denoise = 1;// 1.0 - (startStep / (double)steps); // NOTE: Edit model breaks on denoise<1
            JArray schedulerNode;
            if (scheduler == "turbo")
            {
                string turboNode = CreateNode("SDTurboScheduler", new JObject()
                {
                    ["model"] = model,
                    ["steps"] = steps,
                    ["denoise"] = denoise
                });
                schedulerNode = [turboNode, 0];
            }
            else if (scheduler == "karras")
            {
                string karrasNode = CreateNode("KarrasScheduler", new JObject()
                {
                    ["steps"] = steps,
                    ["sigma_max"] = sigmax <= 0 ? 14.614642 : sigmax,
                    ["sigma_min"] = sigmin <= 0 ? 0.0291675 : sigmin,
                    ["rho"] = UserInput.Get(T2IParamTypes.SamplerRho, 7)
                });
                schedulerNode = [karrasNode, 0];
                if (startStep > 0)
                {
                    string afterStart = CreateNode("SplitSigmas", new JObject()
                    {
                        ["sigmas"] = schedulerNode,
                        ["step"] = startStep
                    });
                    schedulerNode = [afterStart, 1];
                }
            }
            else
            {
                string basicNode = CreateNode("BasicScheduler", new JObject()
                {
                    ["model"] = model,
                    ["steps"] = steps,
                    ["scheduler"] = scheduler,
                    ["denoise"] = denoise
                });
                schedulerNode = [basicNode, 0];
            }
            if (endStep < steps)
            {
                string beforeEnd = CreateNode("SplitSigmas", new JObject()
                {
                    ["sigmas"] = schedulerNode,
                    ["step"] = endStep
                });
                schedulerNode = [beforeEnd, 0];
            }
            string finalSampler = CreateNode("SamplerCustomAdvanced", new JObject()
            {
                ["sampler"] = new JArray() { samplerNode, 0 },
                ["guider"] = new JArray() { cfgGuiderNode, 0 },
                ["sigmas"] = schedulerNode,
                ["latent_image"] = new JArray() { ip2p2condNode, 2 },
                ["noise"] = new JArray() { noiseNode, 0 }
            }, id);
            return finalSampler;
        }
        string firstId = willCascadeFix ? null : id;
        JObject inputs = new()
        {
            ["model"] = model,
            ["noise_seed"] = seed,
            ["steps"] = steps,
            ["cfg"] = cfg,
            // TODO: proper sampler input, and intelligent default scheduler per sampler
            ["sampler_name"] = UserInput.Get(ComfyUIBackendExtension.SamplerParam, defsampler ?? DefaultSampler),
            ["scheduler"] = UserInput.Get(ComfyUIBackendExtension.SchedulerParam, defscheduler ?? DefaultScheduler),
            ["positive"] = pos,
            ["negative"] = neg,
            ["latent_image"] = latent,
            ["start_at_step"] = startStep,
            ["end_at_step"] = endStep,
            ["return_with_leftover_noise"] = returnWithLeftoverNoise ? "enable" : "disable",
            ["add_noise"] = addNoise ? "enable" : "disable"
        };
        string created;
        if (Features.Contains("variation_seed") && !RestrictCustomNodes)
        {
            inputs["var_seed"] = UserInput.Get(T2IParamTypes.VariationSeed, 0);
            inputs["var_seed_strength"] = UserInput.Get(T2IParamTypes.VariationSeedStrength, 0);
            inputs["sigma_min"] = UserInput.Get(T2IParamTypes.SamplerSigmaMin, sigmin);
            inputs["sigma_max"] = UserInput.Get(T2IParamTypes.SamplerSigmaMax, sigmax);
            inputs["rho"] = UserInput.Get(T2IParamTypes.SamplerRho, 7);
            inputs["previews"] = UserInput.Get(T2IParamTypes.NoPreviews) ? "none" : previews ?? DefaultPreviews;
            inputs["tile_sample"] = doTiled;
            inputs["tile_size"] = FinalLoadedModel.StandardWidth <= 0 ? 768 : FinalLoadedModel.StandardWidth;
            created = CreateNode("SwarmKSampler", inputs, firstId);
        }
        else
        {
            created = CreateNode("KSamplerAdvanced", inputs, firstId);
        }
        if (willCascadeFix)
        {
            string stageBCond = CreateNode("StableCascade_StageB_Conditioning", new JObject()
            {
                ["stage_c"] = new JArray() { created, 0 },
                ["conditioning"] = pos
            });
            created = CreateKSampler(cascadeModel, [stageBCond, 0], neg, [latent[0], 1], 1.1, steps, startStep, endStep, seed + 27, returnWithLeftoverNoise, addNoise, sigmin, sigmax, previews ?? previews, defsampler, defscheduler, id, true);
        }
        return created;
    }

    /// <summary>Creates a VAE Encode node.</summary>
    public string CreateVAEEncode(JArray vae, JArray image, string id = null, bool noCascade = false, JArray mask = null)
    {
        if (!noCascade && IsCascade())
        {
            return CreateNode("StableCascade_StageC_VAEEncode", new JObject()
            {
                ["vae"] = vae,
                ["image"] = image,
                ["compression"] = UserInput.Get(T2IParamTypes.CascadeLatentCompression, 32)
            }, id);
        }
        else
        {
            if (mask is not null && (UserInput.Get(T2IParamTypes.UseInpaintingEncode) || (CurrentModelClass()?.ID ?? "").EndsWith("/inpaint")))
            {
                return CreateNode("VAEEncodeForInpaint", new JObject()
                {
                    ["vae"] = vae,
                    ["pixels"] = image,
                    ["mask"] = mask,
                    ["grow_mask_by"] = 6
                }, id);
            }
            return CreateNode("VAEEncode", new JObject()
            {
                ["vae"] = vae,
                ["pixels"] = image
            }, id);
        }
    }

    /// <summary>Creates an Empty Latent Image node.</summary>
    public string CreateEmptyImage(int width, int height, int batchSize, string id = null)
    {
        if (IsCascade())
        {
            return CreateNode("StableCascade_EmptyLatentImage", new JObject()
            {
                ["batch_size"] = batchSize,
                ["compression"] = UserInput.Get(T2IParamTypes.CascadeLatentCompression, 32),
                ["height"] = height,
                ["width"] = width
            }, id);
        }
        else if (IsSD3())
        {
            return CreateNode("EmptySD3LatentImage", new JObject()
            {
                ["batch_size"] = batchSize,
                ["height"] = height,
                ["width"] = width
            }, id);
        }
        else if (UserInput.Get(ComfyUIBackendExtension.ShiftedLatentAverageInit, false))
        {
            double offA = 0, offB = 0, offC = 0, offD = 0;
            switch (FinalLoadedModel.ModelClass?.CompatClass)
            {
                case "stable-diffusion-v1": // https://github.com/Birch-san/sdxl-diffusion-decoder/blob/4ba89847c02db070b766969c0eca3686a1e7512e/script/inference_decoder.py#L112
                case "stable-diffusion-v2":
                    offA = 2.1335;
                    offB = 0.1237;
                    offC = 0.4052;
                    offD = -0.0940;
                    break;
                case "stable-diffusion-xl-v1": // https://huggingface.co/datasets/Birchlabs/sdxl-latents-ffhq
                    offA = -2.8982;
                    offB = -0.9609;
                    offC = 0.2416;
                    offD = -0.3074;
                    break;
            }
            return CreateNode("SwarmOffsetEmptyLatentImage", new JObject()
            {
                ["batch_size"] = batchSize,
                ["height"] = height,
                ["width"] = width,
                ["off_a"] = offA,
                ["off_b"] = offB,
                ["off_c"] = offC,
                ["off_d"] = offD
            }, id);
        }
        else
        {
            return CreateNode("EmptyLatentImage", new JObject()
            {
                ["batch_size"] = batchSize,
                ["height"] = height,
                ["width"] = width
            }, id);
        }
    }

    /// <summary>Enables Differential Diffusion on the current model if is enabled in user settings.</summary>
    public void EnableDifferential()
    {
        if (IsDifferentialDiffusion || UserInput.Get(T2IParamTypes.MaskBehavior, "Differential") != "Differential")
        {
            return;
        }
        IsDifferentialDiffusion = true;
        string diffNode = CreateNode("DifferentialDiffusion", new JObject()
        {
            ["model"] = FinalModel
        });
        FinalModel = [diffNode, 0];
    }

    /// <summary>Creates a "CLIPTextEncode" or equivalent node for the given input.</summary>
    public JArray CreateConditioningDirect(string prompt, JArray clip, T2IModel model, bool isPositive, string id = null)
    {
        string node;
        double mult = isPositive ? 1.5 : 0.8;
        int width = UserInput.Get(T2IParamTypes.Width, 1024);
        int height = UserInput.GetImageHeight();
        bool enhance = UserInput.Get(T2IParamTypes.ModelSpecificEnhancements, true);
        if (Features.Contains("variation_seed") && prompt.Contains('[') && prompt.Contains(']'))
        {
            node = CreateNode("SwarmClipTextEncodeAdvanced", new JObject()
            {
                ["clip"] = clip,
                ["steps"] = UserInput.Get(T2IParamTypes.Steps),
                ["prompt"] = prompt,
                ["width"] = enhance ? (int)Utilities.RoundToPrecision(width * mult, 64) : width,
                ["height"] = enhance ? (int)Utilities.RoundToPrecision(height * mult, 64) : height,
                ["target_width"] = width,
                ["target_height"] = height
            }, id);
        }
        else if (model is not null && model.ModelClass is not null && model.ModelClass.ID == "stable-diffusion-xl-v1-base")
        {
            node = CreateNode("CLIPTextEncodeSDXL", new JObject()
            {
                ["clip"] = clip,
                ["text_g"] = prompt,
                ["text_l"] = prompt,
                ["crop_w"] = 0,
                ["crop_h"] = 0,
                ["width"] = enhance ? (int)Utilities.RoundToPrecision(width * mult, 64) : width,
                ["height"] = enhance ? (int)Utilities.RoundToPrecision(height * mult, 64) : height,
                ["target_width"] = width,
                ["target_height"] = height
            }, id);
        }
        else
        {
            node = CreateNode("CLIPTextEncode", new JObject()
            {
                ["clip"] = clip,
                ["text"] = prompt
            }, id);
        }
        return [node, 0];
    }

    /// <summary>Creates a "CLIPTextEncode" or equivalent node for the given input, with support for '&lt;break&gt;' syntax.</summary>
    public JArray CreateConditioningLine(string prompt, JArray clip, T2IModel model, bool isPositive, string id = null)
    {
        string[] breaks = prompt.Split("<break>", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (breaks.Length <= 1)
        {
            return CreateConditioningDirect(prompt, clip, model, isPositive, id);
        }
        JArray first = CreateConditioningDirect(breaks[0], clip, model, isPositive);
        for (int i = 1; i < breaks.Length; i++)
        {
            JArray second = CreateConditioningDirect(breaks[i], clip, model, isPositive);
            string concatted = CreateNode("ConditioningConcat", new JObject()
            {
                ["conditioning_to"] = first,
                ["conditioning_from"] = second
            });
            first = [concatted, 0];
        }
        return first;
    }

    public record struct RegionHelper(JArray PartCond, JArray Mask);

    /// <summary>Creates a "CLIPTextEncode" or equivalent node for the given input, applying prompt-given conditioning modifiers as relevant.</summary>
    public JArray CreateConditioning(string prompt, JArray clip, T2IModel model, bool isPositive, string firstId = null)
    {
        PromptRegion regionalizer = new(prompt);
        JArray globalCond = CreateConditioningLine(regionalizer.GlobalPrompt, clip, model, isPositive, firstId);
        PromptRegion.Part[] parts = regionalizer.Parts.Where(p => p.Type == PromptRegion.PartType.Object || p.Type == PromptRegion.PartType.Region).ToArray();
        if (parts.IsEmpty())
        {
            return globalCond;
        }
        string gligenModel = UserInput.Get(ComfyUIBackendExtension.GligenModel, "None");
        if (gligenModel != "None")
        {
            string gligenLoader = NodeHelpers.GetOrCreate("gligen_loader", () =>
            {
                return CreateNode("GLIGENLoader", new JObject()
                {
                    ["gligen_name"] = gligenModel
                });
            });
            int width = UserInput.Get(T2IParamTypes.Width, 1024);
            int height = UserInput.GetImageHeight();
            JArray lastCond = globalCond;
            foreach (PromptRegion.Part part in parts)
            {
                string applied = CreateNode("GLIGENTextBoxApply", new JObject()
                {
                    ["gligen_textbox_model"] = new JArray() { gligenLoader, 0 },
                    ["clip"] = clip,
                    ["conditioning_to"] = lastCond,
                    ["text"] = part.Prompt,
                    ["x"] = part.X * width,
                    ["y"] = part.Y * height,
                    ["width"] = part.Width * width,
                    ["height"] = part.Height * height
                });
                lastCond = [applied, 0];
            }
            return lastCond;
        }
        double globalStrength = UserInput.Get(T2IParamTypes.GlobalRegionFactor, 0.5);
        List<RegionHelper> regions = [];
        JArray lastMergedMask = null;
        foreach (PromptRegion.Part part in parts)
        {
            JArray partCond = CreateConditioningLine(part.Prompt, clip, model, isPositive);
            string regionNode = CreateNode("SwarmSquareMaskFromPercent", new JObject()
            {
                ["x"] = part.X,
                ["y"] = part.Y,
                ["width"] = part.Width,
                ["height"] = part.Height,
                ["strength"] = part.Strength
            });
            RegionHelper region = new(partCond, [regionNode, 0]);
            regions.Add(region);
            if (lastMergedMask is null)
            {
                lastMergedMask = region.Mask;
            }
            else
            {
                string overlapped = CreateNode("SwarmOverMergeMasksForOverlapFix", new JObject()
                {
                    ["mask_a"] = lastMergedMask,
                    ["mask_b"] = region.Mask
                });
                lastMergedMask = [overlapped, 0];
            }
        }
        string globalMask = CreateNode("SwarmSquareMaskFromPercent", new JObject()
        {
            ["x"] = 0,
            ["y"] = 0,
            ["width"] = 1,
            ["height"] = 1,
            ["strength"] = 1
        });
        string maskBackground = CreateNode("SwarmExcludeFromMask", new JObject()
        {
            ["main_mask"] = new JArray() { globalMask, 0 },
            ["exclude_mask"] = lastMergedMask
        });
        string backgroundPrompt = string.IsNullOrWhiteSpace(regionalizer.BackgroundPrompt) ? regionalizer.GlobalPrompt : regionalizer.BackgroundPrompt;
        JArray backgroundCond = CreateConditioningLine(backgroundPrompt, clip, model, isPositive);
        string mainConditioning = CreateNode("ConditioningSetMask", new JObject()
        {
            ["conditioning"] = backgroundCond,
            ["mask"] = new JArray() { maskBackground, 0 },
            ["strength"] = 1 - globalStrength,
            ["set_cond_area"] = "default"
        });
        EnableDifferential();
        DebugMask([maskBackground, 0]);
        void DebugMask(JArray mask)
        {
            if (UserInput.Get(ComfyUIBackendExtension.DebugRegionalPrompting))
            {
                string imgNode = CreateNode("MaskToImage", new JObject()
                {
                    ["mask"] = mask
                });
                CreateNode("SwarmSaveImageWS", new JObject()
                {
                    ["images"] = new JArray() { imgNode, 0 }
                });
            }
        }
        foreach (RegionHelper region in regions)
        {
            string overlapped = CreateNode("SwarmCleanOverlapMasksExceptSelf", new JObject()
            {
                ["mask_self"] = region.Mask,
                ["mask_merged"] = lastMergedMask
            });
            DebugMask([overlapped, 0]);
            string regionCond = CreateNode("ConditioningSetMask", new JObject()
            {
                ["conditioning"] = region.PartCond,
                ["mask"] = new JArray() { overlapped, 0 },
                ["strength"] = 1 - globalStrength,
                ["set_cond_area"] = "default"
            });
            mainConditioning = CreateNode("ConditioningCombine", new JObject()
            {
                ["conditioning_1"] = new JArray() { mainConditioning, 0 },
                ["conditioning_2"] = new JArray() { regionCond, 0 }
            });
        }
        string globalCondApplied = CreateNode("ConditioningSetMask", new JObject()
        {
            ["conditioning"] = globalCond,
            ["mask"] = new JArray() { globalMask, 0 },
            ["strength"] = globalStrength,
            ["set_cond_area"] = "default"
        });
        string finalCond = CreateNode("ConditioningCombine", new JObject()
        {
            ["conditioning_1"] = new JArray() { mainConditioning, 0 },
            ["conditioning_2"] = new JArray() { globalCondApplied, 0 }
        });
        return new(finalCond, 0);
    }
}
