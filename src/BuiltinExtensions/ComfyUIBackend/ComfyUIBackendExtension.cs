using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Microsoft.AspNetCore.Builder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using StableSwarmUI.WebAPI;
using System.IO;
using System.Net.Http;

namespace StableSwarmUI.Builtin_ComfyUIBackend;

/// <summary>Main class for the ComfyUI Backend extension.</summary>
public class ComfyUIBackendExtension : Extension
{
    /// <summary>Copy of <see cref="Extension.FilePath"/> for ComfyUI.</summary>
    public static string Folder;

    public record class ComfyCustomWorkflow(string Name, string Workflow, string Prompt, string CustomParams, string ParamValues, string Image, string Description, bool EnableInSimple);

    /// <summary>All current custom workflow IDs mapped to their data.</summary>
    public static ConcurrentDictionary<string, ComfyCustomWorkflow> CustomWorkflows = new();

    /// <summary>Set of all feature-ids supported by ComfyUI backends.</summary>
    public static HashSet<string> FeaturesSupported = ["comfyui", "refiners", "controlnet", "endstepsearly", "seamless", "video", "variation_seed", "freeu", "yolov8"];

    /// <summary>Set of feature-ids that were added presumptively during loading and should be removed if the backend turns out to be missing them.</summary>
    public static HashSet<string> FeaturesDiscardIfNotFound = ["variation_seed", "freeu", "yolov8"];

    /// <summary>Extensible map of ComfyUI Node IDs to supported feature IDs.</summary>
    public static Dictionary<string, string> NodeToFeatureMap = new()
    {
        ["SwarmLoadImageB64"] = "comfy_loadimage_b64",
        ["SwarmSaveImageWS"] = "comfy_saveimage_ws",
        ["SwarmLatentBlendMasked"] = "comfy_latent_blend_masked",
        ["SwarmKSampler"] = "variation_seed",
        ["FreeU"] = "freeu",
        ["AITemplateLoader"] = "aitemplate",
        ["IPAdapter"] = "ipadapter",
        ["IPAdapterApply"] = "ipadapter",
        ["IPAdapterModelLoader"] = "cubiqipadapter",
        ["IPAdapterUnifiedLoader"] = "cubiqipadapterunified",
        ["MiDaS-DepthMapPreprocessor"] = "controlnetpreprocessors",
        ["RIFE VFI"] = "frameinterps",
        ["SwarmYoloDetection"] = "yolov8",
        ["TensorRTLoader"] = "tensorrt"
    };

    /// <inheritdoc/>
    public override void OnPreInit()
    {
        Folder = FilePath;
        LoadWorkflowFiles();
        Program.ModelRefreshEvent += Refresh;
        Program.ModelPathsChangedEvent += OnModelPathsChanged;
        ScriptFiles.Add("Assets/comfy_workflow_editor_helper.js");
        StyleSheetFiles.Add("Assets/comfy_workflow_editor.css");
        T2IParamTypes.FakeTypeProviders.Add(DynamicParamGenerator);
        // Temporary: remove old pycache files where we used to have python files, to prevent Comfy boot errors
        Utilities.RemoveBadPycacheFrom($"{FilePath}/ExtraNodes");
        Utilities.RemoveBadPycacheFrom($"{FilePath}/ExtraNodes/SwarmWebHelper");
        T2IAPI.AlwaysTopKeys.Add("comfyworkflowraw");
        T2IAPI.AlwaysTopKeys.Add("comfyworkflowparammetadata");
        if (Directory.Exists($"{FilePath}/DLNodes/ComfyUI_IPAdapter_plus"))
        {
            FeaturesSupported.UnionWith(["ipadapter", "cubiqipadapterunified"]);
            FeaturesDiscardIfNotFound.UnionWith(["ipadapter", "cubiqipadapterunified"]);
        }
        if (Directory.Exists($"{FilePath}/DLNodes/comfyui_controlnet_aux"))
        {
            FeaturesSupported.UnionWith(["controlnetpreprocessors"]);
            FeaturesDiscardIfNotFound.UnionWith(["controlnetpreprocessors"]);
        }
        if (Directory.Exists($"{FilePath}/DLNodes/ComfyUI-Frame-Interpolation"))
        {
            FeaturesSupported.UnionWith(["frameinterps"]);
            FeaturesDiscardIfNotFound.UnionWith(["frameinterps"]);
        }
        static string[] listModelsFor(string subpath)
        {
            string path = $"{Program.ServerSettings.Paths.ModelRoot}/{subpath}";
            Directory.CreateDirectory(path);
            return [.. Directory.EnumerateFiles(path).Where(f => f.EndsWith(".pth") || f.EndsWith(".pt") || f.EndsWith(".ckpt") || f.EndsWith(".safetensors") || f.EndsWith(".engine")).Select(f => f.Replace('\\', '/').AfterLast('/'))];
        }
        UpscalerModels = [.. UpscalerModels.Concat(listModelsFor("upscale_models").Select(u => $"model-{u}")).Distinct()];
    }

    /// <inheritdoc/>
    public override void OnShutdown()
    {
        T2IParamTypes.FakeTypeProviders.Remove(DynamicParamGenerator);
    }

    /// <summary>Forces all currently running comfy backends to restart.</summary>
    public static async Task RestartAllComfyBackends()
    {
        List<Task> tasks = [];
        foreach (ComfyUIAPIAbstractBackend backend in RunningComfyBackends)
        {
            tasks.Add(Program.Backends.ReloadBackend(backend.BackendData));
        }
        await Task.WhenAll(tasks);
    }

    public static T2IParamType FakeRawInputType = new("comfyworkflowraw", "", "", Type: T2IParamDataType.TEXT, ID: "comfyworkflowraw", FeatureFlag: "comfyui", HideFromMetadata: true), // TODO: Setting to toggle metadata
        FakeParameterMetadata = new("comfyworkflowparammetadata", "", "", Type: T2IParamDataType.TEXT, ID: "comfyworkflowparammetadata", FeatureFlag: "comfyui", HideFromMetadata: true);

    public static SingleCacheAsync<string, JObject> ParameterMetadataCacheHelper = new(s => s.ParseToJson());

    public T2IParamType DynamicParamGenerator(string name, T2IParamInput context)
    {
        try
        {
            if (name == "comfyworkflowraw")
            {
                return FakeRawInputType;
            }
            if (name == "comfyworkflowparammetadata")
            {
                return FakeParameterMetadata;
            }
            if (context.TryGetRaw(FakeParameterMetadata, out object paramMetadataObj))
            {
                JObject paramMetadata = ParameterMetadataCacheHelper.GetValue((string)paramMetadataObj);
                if (paramMetadata.TryGetValue(name, out JToken paramTok))
                {
                    return T2IParamType.FromNet((JObject)paramTok);
                }
                //Logs.Verbose($"Failed to find param metadata for {name} in {paramMetadata.Properties().Select(p => p.Name).JoinString(", ")}");
            }
            if (name.StartsWith("comfyrawworkflowinput") && (context.ValuesInput.ContainsKey("comfyworkflowraw") || context.ValuesInput.ContainsKey("comfyuicustomworkflow")))
            {
                string nameNoPrefix = name.After("comfyrawworkflowinput");
                T2IParamDataType type = FakeRawInputType.Type;
                ParamViewType numberType = ParamViewType.BIG;
                if (nameNoPrefix.StartsWith("seed"))
                {
                    type = T2IParamDataType.INTEGER;
                    numberType = ParamViewType.SEED;
                    nameNoPrefix = nameNoPrefix.After("seed");
                }
                else
                {
                    foreach (T2IParamDataType possible in Enum.GetValues<T2IParamDataType>())
                    {
                        string typeId = possible.ToString().ToLowerFast();
                        if (nameNoPrefix.StartsWith(typeId))
                        {
                            nameNoPrefix = nameNoPrefix.After(typeId);
                            type = possible;
                            break;
                        }
                    }
                }
                T2IParamType resType = FakeRawInputType with { Name = nameNoPrefix, ID = name, HideFromMetadata = false, Type = type, ViewType = numberType };
                if (type == T2IParamDataType.MODEL)
                {
                    static string cleanup(string _, string val)
                    {
                        val = val.Replace('\\', '/');
                        while (val.Contains("//"))
                        {
                            val = val.Replace("//", "/");
                        }
                        val = val.Replace('/', Path.DirectorySeparatorChar);
                        return val;
                    }
                    resType = resType with { Clean = cleanup };
                }
                return resType;
            }
        }
        catch (Exception e)
        {
            Logs.Error($"Error generating dynamic Comfy param {name}: {e}");
        }
        return null;
    }

    public static IEnumerable<ComfyUIAPIAbstractBackend> RunningComfyBackends => Program.Backends.RunningBackendsOfType<ComfyUIAPIAbstractBackend>();

    public static string[] ExampleWorkflowNames;

    public void LoadWorkflowFiles()
    {
        CustomWorkflows.Clear();
        Directory.CreateDirectory($"{FilePath}/CustomWorkflows");
        Directory.CreateDirectory($"{FilePath}/CustomWorkflows/Examples");
        string[] getCustomFlows(string path) => [.. Directory.EnumerateFiles($"{FilePath}/{path}", "*.*", new EnumerationOptions() { RecurseSubdirectories = true }).Select(f => f.Replace('\\', '/').After($"/{path}/")).Order()];
        ExampleWorkflowNames = getCustomFlows("ExampleWorkflows");
        string[] customFlows = getCustomFlows("CustomWorkflows");
        bool anyCopied = false;
        foreach (string workflow in ExampleWorkflowNames.Where(f => f.EndsWith(".json")))
        {
            if (!customFlows.Contains($"Examples/{workflow}") && !customFlows.Contains($"Examples/{workflow}.deleted"))
            {
                File.Copy($"{FilePath}/ExampleWorkflows/{workflow}", $"{FilePath}/CustomWorkflows/Examples/{workflow}");
                anyCopied = true;
            }
        }
        if (anyCopied)
        {
            customFlows = getCustomFlows("CustomWorkflows");
        }
        foreach (string workflow in customFlows.Where(f => f.EndsWith(".json")))
        {
            CustomWorkflows.TryAdd(workflow.BeforeLast('.'), null);
        }
    }

    public static ComfyCustomWorkflow GetWorkflowByName(string name)
    {
        if (!CustomWorkflows.TryGetValue(name, out ComfyCustomWorkflow workflow))
        {
            return null;
        }
        if (workflow is not null)
        {
            return workflow;
        }
        string path = $"{Folder}/CustomWorkflows/{name}.json";
        if (!File.Exists(path))
        {
            CustomWorkflows.TryRemove(name, out _);
            return null;
        }
        JObject json = File.ReadAllText(path).ParseToJson();
        string getStringFor(string key)
        {
            if (!json.TryGetValue(key, out JToken data))
            {
                return null;
            }
            if (data.Type == JTokenType.String)
            {
                return data.ToString();
            }
            return data.ToString(Formatting.None);
        }
        string workflowData = getStringFor("workflow");
        string prompt = getStringFor("prompt");
        string customParams = getStringFor("custom_params");
        string paramValues = getStringFor("param_values");
        string image = getStringFor("image") ?? "/imgs/model_placeholder.jpg";
        string description = getStringFor("description");
        bool enableInSimple = json.TryGetValue("enable_in_simple", out JToken enableInSimpleTok) && enableInSimpleTok.ToObject<bool>();
        workflow = new(name, workflowData, prompt, customParams, paramValues, image, description, enableInSimple);
        CustomWorkflows[name] = workflow;
        return workflow;
    }

    public void Refresh()
    {
        List<Task> tasks = [];
        try
        {
            ComfyUIRedirectHelper.ObjectInfoReadCacher.ForceExpire();
            LoadWorkflowFiles();
            foreach (ComfyUIAPIAbstractBackend backend in RunningComfyBackends.ToArray())
            {
                tasks.Add(backend.LoadValueSet(5));
            }
        }
        catch (Exception ex)
        {
            Logs.Error($"Error refreshing ComfyUI: {ex}");
        }
        if (!tasks.Any())
        {
            return;
        }
        try
        {
            Task.WaitAll([.. tasks], Utilities.TimedCancel(TimeSpan.FromMinutes(0.5)));
        }
        catch (Exception ex)
        {
            Logs.Debug("ComfyUI refresh failed, will retry in background");
            Logs.Verbose($"Error refreshing ComfyUI: {ex}");
            Utilities.RunCheckedTask(() =>
            {
                try
                {
                    Task.WaitAll([.. tasks], Utilities.TimedCancel(TimeSpan.FromMinutes(5)));
                }
                catch (Exception ex2)
                {
                    Logs.Error($"Error refreshing ComfyUI: {ex2}");
                }
            });
        }
    }

    public void OnModelPathsChanged()
    {
        foreach (ComfyUISelfStartBackend backend in Program.Backends.RunningBackendsOfType<ComfyUISelfStartBackend>())
        {
            if (backend.IsEnabled)
            {
                Program.Backends.ReloadBackend(backend.BackendData).Wait(Program.GlobalProgramCancel);
            }
        }
    }

    public static async Task RunArbitraryWorkflowOnFirstBackend(string workflow, Action<object> takeRawOutput, bool allowRemote = true)
    {
        ComfyUIAPIAbstractBackend backend = RunningComfyBackends.FirstOrDefault(b => allowRemote || b is ComfyUISelfStartBackend) ?? throw new InvalidOperationException("No available ComfyUI Backend to run this operation");
        await backend.AwaitJobLive(workflow, "0", takeRawOutput, new(null), Program.GlobalProgramCancel);
    }

    public static LockObject ValueAssignmentLocker = new();

    public static void AssignValuesFromRaw(JObject rawObjectInfo)
    {
        lock (ValueAssignmentLocker)
        {
            if (rawObjectInfo.TryGetValue("UpscaleModelLoader", out JToken modelLoader))
            {
                UpscalerModels = UpscalerModels.Concat(modelLoader["input"]["required"]["model_name"][0].Select(u => $"model-{u}")).Distinct().ToList();
            }
            if (rawObjectInfo.TryGetValue("KSampler", out JToken ksampler))
            {
                Samplers = Samplers.Concat(ksampler["input"]["required"]["sampler_name"][0].Select(u => $"{u}")).Distinct().ToList();
                Schedulers = Schedulers.Concat(ksampler["input"]["required"]["scheduler"][0].Select(u => $"{u}")).Distinct().ToList();
            }
            if (rawObjectInfo.TryGetValue("SwarmKSampler", out JToken swarmksampler))
            {
                Samplers = Samplers.Concat(swarmksampler["input"]["required"]["sampler_name"][0].Select(u => $"{u}")).Distinct().ToList();
                Schedulers = Schedulers.Concat(swarmksampler["input"]["required"]["scheduler"][0].Select(u => $"{u}")).Distinct().ToList();
            }
            if (rawObjectInfo.TryGetValue("IPAdapterUnifiedLoader", out JToken ipadapterCubiqUnified))
            {
                IPAdapterModels = IPAdapterModels.Concat(ipadapterCubiqUnified["input"]["required"]["preset"][0].Select(m => $"{m}")).Distinct().ToList();
            }
            else if (rawObjectInfo.TryGetValue("IPAdapter", out JToken ipadapter) && (ipadapter["input"]["required"] as JObject).TryGetValue("model_name", out JToken ipAdapterModelName))
            {
                IPAdapterModels = IPAdapterModels.Concat(ipAdapterModelName[0].Select(m => $"{m}")).Distinct().ToList();
            }
            else if (rawObjectInfo.TryGetValue("IPAdapterModelLoader", out JToken ipadapterCubiq))
            {
                IPAdapterModels = IPAdapterModels.Concat(ipadapterCubiq["input"]["required"]["ipadapter_file"][0].Select(m => $"{m}")).Distinct().ToList();
            }
            if (rawObjectInfo.TryGetValue("IPAdapterUnifiedLoaderFaceID", out JToken ipadapterCubiqUnifiedFace))
            {
                IPAdapterModels = IPAdapterModels.Concat(ipadapterCubiqUnifiedFace["input"]["required"]["preset"][0].Select(m => $"{m}")).Distinct().ToList();
            }
            if (rawObjectInfo.TryGetValue("GLIGENLoader", out JToken gligenLoader))
            {
                GligenModels = GligenModels.Concat(gligenLoader["input"]["required"]["gligen_name"][0].Select(m => $"{m}")).Distinct().ToList();
            }
            if (rawObjectInfo.TryGetValue("SwarmYoloDetection", out JToken yoloDetection))
            {
                YoloModels = YoloModels.Concat(yoloDetection["input"]["required"]["model_name"][0].Select(m => $"{m}")).Distinct().ToList();
            }
            foreach ((string key, JToken data) in rawObjectInfo)
            {
                if (data["category"].ToString() == "image/preprocessors")
                {
                    ControlNetPreprocessors[key] = data;
                }
                else if (key.EndsWith("Preprocessor"))
                {
                    ControlNetPreprocessors[key] = data;
                }
                if (NodeToFeatureMap.TryGetValue(key, out string featureId))
                {
                    FeaturesSupported.Add(featureId);
                    FeaturesDiscardIfNotFound.Remove(featureId);
                }
            }
            foreach (string feature in FeaturesDiscardIfNotFound)
            {
                FeaturesSupported.Remove(feature);
            }
        }
    }

    public static T2IRegisteredParam<string> WorkflowParam, CustomWorkflowParam, SamplerParam, SchedulerParam, RefinerUpscaleMethod, UseIPAdapterForRevision, VideoPreviewType, VideoFrameInterpolationMethod, GligenModel, YoloModelInternal;

    public static T2IRegisteredParam<bool> AITemplateParam, DebugRegionalPrompting, ShiftedLatentAverageInit;

    public static T2IRegisteredParam<double> IPAdapterWeight, SelfAttentionGuidanceScale, SelfAttentionGuidanceSigmaBlur;

    public static T2IRegisteredParam<int> RefinerHyperTile, VideoFrameInterpolationMultiplier;

    public static T2IRegisteredParam<string>[] ControlNetPreprocessorParams = new T2IRegisteredParam<string>[3];

    public static List<string> UpscalerModels = ["pixel-lanczos", "pixel-bicubic", "pixel-area", "pixel-bilinear", "pixel-nearest-exact", "latent-bislerp", "latent-bicubic", "latent-area", "latent-bilinear", "latent-nearest-exact"],
        Samplers = ["euler", "euler_ancestral", "heun", "dpm_2", "dpm_2_ancestral", "lms", "dpm_fast", "dpm_adaptive", "dpmpp_2s_ancestral", "dpmpp_sde", "dpmpp_sde_gpu", "dpmpp_2m", "dpmpp_2m_sde", "dpmpp_2m_sde_gpu", "dpmpp_3m_sde", "dpmpp_3m_sde_gpu", "ddim", "ddpm", "heunpp2", "lcm", "uni_pc", "uni_pc_bh2"],
        Schedulers = ["normal", "karras", "exponential", "simple", "ddim_uniform", "sgm_uniform", "turbo", "align_your_steps"];

    public static List<string> IPAdapterModels = ["None"];

    public static List<string> GligenModels = ["None"];

    public static List<string> YoloModels = [];

    public static ConcurrentDictionary<string, JToken> ControlNetPreprocessors = new() { ["None"] = null };

    public static T2IParamGroup ComfyGroup, ComfyAdvancedGroup;

    /// <inheritdoc/>
    public override void OnInit()
    {
        UseIPAdapterForRevision = T2IParamTypes.Register<string>(new("Use IP-Adapter", $"Select an IP-Adapter model to use IP-Adapter for image-prompt input handling.\nModels will automatically be downloaded when you first use them.\n<a href=\"{Utilities.RepoDocsRoot}/Features/IPAdapter-ReVision.md\">See more docs here.</a>",
            "None", IgnoreIf: "None", FeatureFlag: "ipadapter", GetValues: _ => IPAdapterModels, Group: T2IParamTypes.GroupRevision, OrderPriority: 15, ChangeWeight: 1
            ));
        IPAdapterWeight = T2IParamTypes.Register<double>(new("IP-Adapter Weight", "Weight to use with IP-Adapter (if enabled).",
            "1", Min: -1, Max: 3, Step: 0.05, IgnoreIf: "1", FeatureFlag: "ipadapter", Group: T2IParamTypes.GroupRevision, ViewType: ParamViewType.SLIDER, OrderPriority: 16
            ));
        ComfyGroup = new("ComfyUI", Toggles: false, Open: false);
        ComfyAdvancedGroup = new("ComfyUI Advanced", Toggles: false, IsAdvanced: true, Open: false);
        CustomWorkflowParam = T2IParamTypes.Register<string>(new("[ComfyUI] Custom Workflow", "What custom workflow to use in ComfyUI (built in the Comfy Workflow Editor tab).\nGenerally, do not use this directly.",
            "", Toggleable: true, FeatureFlag: "comfyui", Group: ComfyGroup, IsAdvanced: true, ValidateValues: false, ChangeWeight: 8,
            GetValues: (_) => [.. CustomWorkflows.Keys.Order()],
            Clean: (_, val) => CustomWorkflows.ContainsKey(val) ? $"PARSED%{val}%{ComfyUIWebAPI.ReadCustomWorkflow(val)["prompt"]}" : val,
            MetadataFormat: v => v.StartsWith("PARSED%") ? v.After("%").Before("%") : v
            ));
        SamplerParam = T2IParamTypes.Register<string>(new("Sampler", "Sampler type (for ComfyUI)\nGenerally, 'Euler' is fine, but for SD1 and SDXL 'dpmpp_2m' is popular when paired with the 'karras' scheduler.",
            "euler", Toggleable: true, FeatureFlag: "comfyui", Group: T2IParamTypes.GroupSampling, OrderPriority: -5,
            GetValues: (_) => Samplers
            ));
        SchedulerParam = T2IParamTypes.Register<string>(new("Scheduler", "Scheduler type (for ComfyUI)\nGoes with the Sampler parameter above.",
            "normal", Toggleable: true, FeatureFlag: "comfyui", Group: T2IParamTypes.GroupSampling, OrderPriority: -4,
            GetValues: (_) => Schedulers
            ));
        AITemplateParam = T2IParamTypes.Register<bool>(new("Enable AITemplate", "If checked, enables AITemplate for ComfyUI generations (UNet only). Only compatible with some GPUs.",
            "false", IgnoreIf: "false", FeatureFlag: "aitemplate", Group: ComfyGroup, ChangeWeight: 5
            ));
        SelfAttentionGuidanceScale = T2IParamTypes.Register<double>(new("Self-Attention Guidance Scale", "Scale for Self-Attention Guidance.\n''Self-Attention Guidance (SAG) uses the intermediate self-attention maps of diffusion models to enhance their stability and efficacy.\nSpecifically, SAG adversarially blurs only the regions that diffusion models attend to at each iteration and guides them accordingly.''\nDefaults to 0.5.",
            "0.5", Min: -2, Max: 5, Step: 0.1, FeatureFlag: "comfyui", Group: T2IParamTypes.GroupAdvancedSampling, IsAdvanced: true, Toggleable: true, ViewType: ParamViewType.SLIDER
            ));
        SelfAttentionGuidanceSigmaBlur = T2IParamTypes.Register<double>(new("Self-Attention Guidance Sigma Blur", "Blur-sigma for Self-Attention Guidance.\nDefaults to 2.0.",
            "2", Min: 0, Max: 10, Step: 0.25, FeatureFlag: "comfyui", Group: T2IParamTypes.GroupAdvancedSampling, IsAdvanced: true, Toggleable: true, ViewType: ParamViewType.SLIDER
            ));
        RefinerUpscaleMethod = T2IParamTypes.Register<string>(new("Refiner Upscale Method", "How to upscale the image, if upscaling is used.",
            "pixel-lanczos", Group: T2IParamTypes.GroupRefiners, OrderPriority: -1, FeatureFlag: "comfyui", ChangeWeight: 1,
            GetValues: (_) => UpscalerModels
            ));
        for (int i = 0; i < 3; i++)
        {
            ControlNetPreprocessorParams[i] = T2IParamTypes.Register<string>(new($"ControlNet{T2IParamTypes.Controlnets[i].NameSuffix} Preprocessor", "The preprocessor to use on the ControlNet input image.\nIf toggled off, will be automatically selected.\nUse 'None' to disable preprocessing.",
                "None", Toggleable: true, FeatureFlag: "controlnet", Group: T2IParamTypes.Controlnets[i].Group, OrderPriority: 3, GetValues: (_) => [.. ControlNetPreprocessors.Keys.Order().OrderBy(v => v == "None" ? -1 : 0)], ChangeWeight: 2
                ));
        }
        DebugRegionalPrompting = T2IParamTypes.Register<bool>(new("Debug Regional Prompting", "If checked, outputs masks from regional prompting for debug reasons.",
            "false", IgnoreIf: "false", FeatureFlag: "comfyui", VisibleNormally: false
            ));
        RefinerHyperTile = T2IParamTypes.Register<int>(new("Refiner HyperTile", "The size of hypertiles to use for the refining stage.\nHyperTile is a technique to speed up sampling of large images by tiling the image and batching the tiles.\nThis is useful when using SDv1 models as the refiner. SDXL-Base models do not benefit as much.",
            "256", Min: 64, Max: 2048, Step: 32, Toggleable: true, IsAdvanced: true, FeatureFlag: "comfyui", ViewType: ParamViewType.POT_SLIDER, Group: T2IParamTypes.GroupRefiners, OrderPriority: 20
            ));
        VideoPreviewType = T2IParamTypes.Register<string>(new("Video Preview Type", "How to display previews for generating videos.\n'Animate' shows a low-res animated video preview.\n'iterate' shows one frame at a time while it goes.\n'one' displays just the first frame.\n'none' disables previews.",
            "animate", FeatureFlag: "comfyui", Group: T2IParamTypes.GroupVideo, GetValues: (_) => ["animate", "iterate", "one", "none"]
            ));
        VideoFrameInterpolationMethod = T2IParamTypes.Register<string>(new("Video Frame Interpolation Method", "How to interpolate frames in the video.\n'RIFE' or 'FILM' are two different decent interpolation model options.",
            "RIFE", FeatureFlag: "frameinterps", Group: T2IParamTypes.GroupVideo, GetValues: (_) => ["RIFE", "FILM"], OrderPriority: 32
            ));
        VideoFrameInterpolationMultiplier = T2IParamTypes.Register<int>(new("Video Frame Interpolation Multiplier", "How many frames to interpolate between each frame in the video.\nHigher values are smoother, but make take significant time to save the output, and may have quality artifacts.",
            "1", IgnoreIf: "1", Min: 1, Max: 10, Step: 1, FeatureFlag: "frameinterps", Group: T2IParamTypes.GroupVideo, OrderPriority: 33
            ));
        GligenModel = T2IParamTypes.Register<string>(new("GLIGEN Model", "Optionally use a GLIGEN model.\nGLIGEN is only compatible with SDv1 at time of writing.",
            "None", IgnoreIf: "None", FeatureFlag: "comfyui", Group: T2IParamTypes.GroupRegionalPrompting, GetValues: (_) => GligenModels
            ));
        ShiftedLatentAverageInit = T2IParamTypes.Register<bool>(new("Shifted Latent Average Init", "If checked, shifts the empty latent to use a mean-average per-channel latent value (as calculated by Birchlabs).\nIf unchecked, default behavior of zero-init latents are used.\nThis can potentially improve the color range or even general quality on SDv1, SDv2, and SDXL models.\nNote that the effect is very minor.",
            "false", IgnoreIf: "false", FeatureFlag: "comfyui", Group: T2IParamTypes.GroupAdvancedSampling, IsAdvanced: true
            ));
        YoloModelInternal = T2IParamTypes.Register<string>(new("YOLO Model Internal", "Parameter for internally tracking YOLOv8 models.\nThis is not for real usage, it is just to expose the list to the UI handler.",
            "", IgnoreIf: "", FeatureFlag: "yolov8", Group: ComfyAdvancedGroup, GetValues: (_) => YoloModels, Toggleable: true, IsAdvanced: true, AlwaysRetain: true, VisibleNormally: false
            ));
        Program.Backends.RegisterBackendType<ComfyUIAPIBackend>("comfyui_api", "ComfyUI API By URL", "A backend powered by a pre-existing installation of ComfyUI, referenced via API base URL.", true);
        Program.Backends.RegisterBackendType<ComfyUISelfStartBackend>("comfyui_selfstart", "ComfyUI Self-Starting", "A backend powered by a pre-existing installation of the ComfyUI, automatically launched and managed by this UI server.", isStandard: true);
        ComfyUIWebAPI.Register();
    }

    public override void OnPreLaunch()
    {
        WebServer.WebApp.Map("/ComfyBackendDirect/{*Path}", ComfyUIRedirectHelper.ComfyBackendDirectHandler);
    }

    public record struct ComfyBackendData(HttpClient Client, string Address, AbstractT2IBackend Backend);

    public static IEnumerable<ComfyBackendData> ComfyBackendsDirect()
    {
        foreach (ComfyUIAPIAbstractBackend backend in RunningComfyBackends)
        {
            yield return new(ComfyUIAPIAbstractBackend.HttpClient, backend.Address, backend);
        }
        foreach (SwarmSwarmBackend swarmBackend in Program.Backends.RunningBackendsOfType<SwarmSwarmBackend>().Where(b => b.RemoteBackendTypes.Any(b => b.StartsWith("comfyui_"))))
        {
            yield return new(SwarmSwarmBackend.HttpClient, $"{swarmBackend.Settings.Address}/ComfyBackendDirect", swarmBackend);
        }
    }
}
