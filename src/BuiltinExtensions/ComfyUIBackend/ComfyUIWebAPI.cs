using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using StableSwarmUI.WebAPI;
using System.IO;
using System.Net.WebSockets;

namespace StableSwarmUI.Builtin_ComfyUIBackend;

public static class ComfyUIWebAPI
{
    public static void Register()
    {
        API.RegisterAPICall(ComfySaveWorkflow, true);
        API.RegisterAPICall(ComfyReadWorkflow);
        API.RegisterAPICall(ComfyListWorkflows);
        API.RegisterAPICall(ComfyDeleteWorkflow, true);
        API.RegisterAPICall(ComfyGetGeneratedWorkflow);
        API.RegisterAPICall(DoLoraExtractionWS, true);
        API.RegisterAPICall(ComfyEnsureRefreshable);
        API.RegisterAPICall(ComfyInstallFeatures, true);
        API.RegisterAPICall(DoTensorRTCreateWS, true);
    }

    /// <summary>API route to save a comfy workflow object to persistent file.</summary>
    public static async Task<JObject> ComfySaveWorkflow(string name, string workflow, string prompt, string custom_params, string param_values, string image, string description = "", bool enable_in_simple = false)
    {
        string cleaned = Utilities.StrictFilenameClean(name);
        string path = $"{ComfyUIBackendExtension.Folder}/CustomWorkflows/{cleaned}.json";
        Directory.CreateDirectory(Directory.GetParent(path).FullName);
        if (!string.IsNullOrWhiteSpace(image))
        {
            image = Image.FromDataString(image).ToMetadataFormat();
        }
        else if (ComfyUIBackendExtension.CustomWorkflows.ContainsKey(path))
        {
            ComfyUIBackendExtension.ComfyCustomWorkflow oldFlow = ComfyUIBackendExtension.GetWorkflowByName(path);
            image = oldFlow.Image;
        }
        if (string.IsNullOrWhiteSpace(image))
        {
            image = "/imgs/model_placeholder.jpg";
        }
        ComfyUIBackendExtension.CustomWorkflows[cleaned] = new ComfyUIBackendExtension.ComfyCustomWorkflow(cleaned, workflow, prompt, custom_params, param_values, image, description, enable_in_simple);
        JObject data = new()
        {
            ["workflow"] = workflow.ParseToJson(),
            ["prompt"] = prompt.ParseToJson(),
            ["custom_params"] = custom_params.ParseToJson(),
            ["param_values"] = param_values.ParseToJson(),
            ["image"] = image,
            ["description"] = description ?? "",
            ["enable_in_simple"] = enable_in_simple
        };
        File.WriteAllBytes(path, data.ToString().EncodeUTF8());
        return new JObject() { ["success"] = true };
    }

    /// <summary>Method to directly read a custom workflow file.</summary>
    public static JObject ReadCustomWorkflow(string name)
    {
        string path = Utilities.StrictFilenameClean(name);
        ComfyUIBackendExtension.ComfyCustomWorkflow workflow = ComfyUIBackendExtension.GetWorkflowByName(path);
        if (workflow is null)
        {
            return new JObject() { ["error"] = "Unknown custom workflow name." };
        }
        return new JObject()
        {
            ["workflow"] = workflow.Workflow,
            ["prompt"] = workflow.Prompt,
            ["custom_params"] = workflow.CustomParams,
            ["image"] = workflow.Image ?? "/imgs/model_placeholder.jpg",
            ["description"] = workflow.Description ?? "",
            ["enable_in_simple"] = workflow.EnableInSimple
        };
    }

    /// <summary>API route to read a comfy workflow object from persistent file.</summary>
    public static async Task<JObject> ComfyReadWorkflow(string name)
    {
        JObject val = ReadCustomWorkflow(name);
        if (val.ContainsKey("error"))
        {
            return val;
        }
        return new JObject() { ["result"] = val };
    }

    /// <summary>API route to read a list of available Comfy custom workflows.</summary>
    public static async Task<JObject> ComfyListWorkflows()
    {
        return new JObject() { ["workflows"] = JToken.FromObject(ComfyUIBackendExtension.CustomWorkflows.Keys.ToList()
            .Select(ComfyUIBackendExtension.GetWorkflowByName).OrderBy(w => w.Name).Select(w => new JObject()
            {
                ["name"] = w.Name,
                ["image"] = w.Image ?? "/imgs/model_placeholder.jpg",
                ["description"] = w.Description,
                ["enable_in_simple"] = w.EnableInSimple
            }).ToList()) };
    }

    /// <summary>API route to read a delete a saved Comfy custom workflows.</summary>
    public static async Task<JObject> ComfyDeleteWorkflow(string name)
    {
        string path = Utilities.StrictFilenameClean(name);
        if (!ComfyUIBackendExtension.CustomWorkflows.Remove(path, out _))
        {
            return new JObject() { ["error"] = "Unknown custom workflow name." };
        }
        string fullPath = $"{ComfyUIBackendExtension.Folder}/CustomWorkflows/{path}.json";
        if (!File.Exists(fullPath))
        {
            return new JObject() { ["error"] = "Unknown custom workflow name." };
        }
        File.Delete(fullPath);
        Logs.Debug($"check {path} against {ComfyUIBackendExtension.ExampleWorkflowNames.JoinString(", ")}");
        if (ComfyUIBackendExtension.ExampleWorkflowNames.Contains(path.After("Examples/") + ".json"))
        {
            File.WriteAllText($"{fullPath}.deleted", "deleted-by-user");
        }
        return new JObject() { ["success"] = true };
    }

    /// <summary>API route to get a generated workflow for a T2I input.</summary>
    public static async Task<JObject> ComfyGetGeneratedWorkflow(Session session, JObject rawInput)
    {
        T2IParamInput input;
        try
        {
            input = T2IAPI.RequestToParams(session, rawInput);
        }
        catch (InvalidOperationException ex)
        {
            return new JObject() { ["error"] = ex.Message };
        }
        catch (InvalidDataException ex)
        {
            return new JObject() { ["error"] = ex.Message };
        }
        input.PreparsePromptLikes();
        ComfyUIAPIAbstractBackend backend = ComfyUIBackendExtension.ComfyBackendsDirect().FirstOrDefault().Backend as ComfyUIAPIAbstractBackend;
        string format = backend.SupportedFeatures.Contains("folderbackslash") ? "\\" : "/";
        string flow = ComfyUIAPIAbstractBackend.CreateWorkflow(input, w => w, format, features: backend.SupportedFeatures.ToHashSet());
        return new JObject() { ["workflow"] = flow };
    }

    /// <summary>API route to ensure that a ComfyUI refresh hit will actually do a native refresh.</summary>
    public static async Task<JObject> ComfyEnsureRefreshable(Session session)
    {
        ComfyUIRedirectHelper.ObjectInfoReadCacher.ForceExpire();
        return new JObject() { ["success"] = true };
    }

    public static SemaphoreSlim MultiInstallLock = new(1, 1);

    /// <summary>API route to ensure to install a given ComfyUI custom node feature.</summary>
    public static async Task<JObject> ComfyInstallFeatures(Session session, string feature)
    {
        await MultiInstallLock.WaitAsync(Program.GlobalProgramCancel);
        try
        {
            if (Program.Backends.RunningBackendsOfType<ComfyUISelfStartBackend>().IsEmpty())
            {
                Logs.Warning($"User {session.User.UserID} tried to install feature '{feature}' but have no comfy self-start backends.");
                return new JObject() { ["error"] = $"Cannot install Comfy features as this Swarm instance has no running ComfyUI Self-Start backends currently." };
            }
            static async Task<JObject> doRepo(string path)
            {
                bool didRestart = await ComfyUISelfStartBackend.EnsureNodeRepo(path);
                if (!didRestart)
                {
                    _ = Utilities.RunCheckedTask(ComfyUIBackendExtension.RestartAllComfyBackends);
                }
                return new JObject() { ["success"] = true };
            }
            feature = feature.ToLowerFast().Trim();
            if (feature == "ipadapter") { return await doRepo("https://github.com/cubiq/ComfyUI_IPAdapter_plus"); }
            else if (feature == "controlnet_preprocessors") { return await doRepo("https://github.com/Fannovel16/comfyui_controlnet_aux"); }
            else if (feature == "frame_interpolation") { return await doRepo("https://github.com/Fannovel16/ComfyUI-Frame-Interpolation"); }
            else if (feature == "comfyui_tensorrt") { return await doRepo("https://github.com/comfyanonymous/ComfyUI_TensorRT"); }
            else
            {
                Logs.Warning($"User {session.User.UserID} tried to install unknown feature '{feature}'.");
                return new JObject() { ["error"] = $"Unknown feature ID {feature}." };
            }
        }
        finally
        {
            MultiInstallLock.Release();
        }
    }

    public static Dictionary<string, int> AspectRangeToMultiplier = new()
    {
        ["Exact"] = 1,
        ["2x"] = 2,
        ["4x"] = 4
    };

    public static Dictionary<string, string> ArchitecturesTRTCompat = new()
    {
        ["stable-diffusion-v1"] = "sd1.x",
        ["stable-diffusion-v2-768-v"] = "sd2.x-768v",
        ["stable-diffusion-xl-v1-base"] = "sdxl_base",
        ["stable-diffusion-xl-v0_9-base"] = "sdxl_base",
        ["stable-diffusion-xl-turbo-v1"] = "sdxl_base",
        ["stable-diffusion-xl-v1-refiner"] = "sdxl_refiner",
        ["stable-video-diffusion-img2vid-v1"] = "svd",
        ["stable-diffusion-v3-medium"] = "sd3"
    };

    /// <summary>API route to create a TensorRT model.</summary>
    public static async Task<JObject> DoTensorRTCreateWS(Session session, WebSocket ws, string model, string aspect, string aspectRange, int optBatch, int maxBatch)
    {
        if (ModelsAPI.TryGetRefusalForModel(session, model, out JObject refusal))
        {
            await ws.SendJson(refusal, API.WebsocketTimeout);
            return null;
        }
        model = T2IParamTypes.GetBestModelInList(model, Program.MainSDModels.Models.Keys);
        T2IModel modelData = Program.MainSDModels.Models.GetValueOrDefault(model);
        if (modelData is null)
        {
            await ws.SendJson(new JObject() { ["error"] = "Unknown input model name." }, API.WebsocketTimeout);
            return null;
        }
        if (!ArchitecturesTRTCompat.ContainsKey(modelData.ModelClass?.ID))
        {
            await ws.SendJson(new JObject() { ["error"] = "This model does not have an Architecture ID listed as compatible with TensorRT (v1, v2-768-v, XL-v1-base, XL-v1-refiner, stable-video-diffusion)." }, API.WebsocketTimeout);
            return null;
        }
        if (optBatch < 1 || maxBatch < 1 || optBatch > 64 || maxBatch > 64)
        {
            await ws.SendJson(new JObject() { ["error"] = "Batch size must be from 1 to 64." }, API.WebsocketTimeout);
            return null;
        }
        (int aspectX, int aspectY) = T2IParamTypes.AspectRatioToSizeReference(aspect);
        if (aspectX <= 0 || aspectY <= 0)
        {
            await ws.SendJson(new JObject() { ["error"] = "Invalid aspect ratio." }, API.WebsocketTimeout);
            return null;
        }
        if (!AspectRangeToMultiplier.TryGetValue(aspectRange, out int rangeMult))
        {
            await ws.SendJson(new JObject() { ["error"] = "Invalid aspect range." }, API.WebsocketTimeout);
            return null;
        }
        int standardWidth = modelData.StandardWidth <= 0 ? 1024 : modelData.StandardWidth;
        int standardHeight = modelData.StandardHeight <= 0 ? 1024 : modelData.StandardHeight;
        (int prefX, int prefY) = Utilities.ResToModelFit(aspectX, aspectY, standardWidth * standardHeight);
        (int minX, int minY) = Utilities.ResToModelFit(aspectX, aspectY, (standardWidth / rangeMult) * (standardHeight / rangeMult));
        (int maxX, int maxY) = Utilities.ResToModelFit(aspectX, aspectY, (standardWidth * rangeMult) * (standardHeight * rangeMult));
        if (ComfyUIBackendExtension.RunningComfyBackends.FirstOrDefault(b => b is ComfyUISelfStartBackend) is not ComfyUISelfStartBackend backend)
        {
            await ws.SendJson(new JObject() { ["error"] = "No ComfyUI self-start backend available." }, API.WebsocketTimeout);
            return null;
        }
        string format = backend.ModelFolderFormat;
        string prefix = $"{Guid.NewGuid()}";
        JObject workflow = new()
        {
            ["4"] = new JObject()
            {
                ["class_type"] = "CheckpointLoaderSimple",
                ["inputs"] = new JObject()
                {
                    ["ckpt_name"] = modelData.ToString(format)
                }
            }
        };
        if (rangeMult == 1)
        {
            workflow["10"] = new JObject()
            {
                ["class_type"] = "STATIC_TRT_MODEL_CONVERSION",
                ["inputs"] = new JObject()
                {
                    ["model"] = new JArray() { "4", 0 },
                    ["filename_prefix"] = $"swarmtemptrt/{prefix}/",
                    ["batch_size_opt"] = optBatch,
                    ["height_opt"] = prefY,
                    ["width_opt"] = prefX,
                    ["context_opt"] = 1,
                    ["num_video_frames"] = 25
                }
            };
        }
        else
        {
            workflow["10"] = new JObject()
            {
                ["class_type"] = "DYNAMIC_TRT_MODEL_CONVERSION",
                ["inputs"] = new JObject()
                {
                    ["model"] = new JArray() { "4", 0 },
                    ["filename_prefix"] = $"swarmtemptrt/{prefix}/",
                    ["batch_size_min"] = 1,
                    ["batch_size_opt"] = optBatch,
                    ["batch_size_max"] = maxBatch,
                    ["height_min"] = Math.Clamp(minY, 256, 4096),
                    ["height_opt"] = prefY,
                    ["height_max"] = Math.Clamp(maxY, 256, 4096),
                    ["width_min"] = Math.Clamp(minX, 256, 4096),
                    ["width_opt"] = prefX,
                    ["width_max"] = Math.Clamp(maxX, 256, 4096),
                    ["context_min"] = 1,
                    ["context_opt"] = 1,
                    ["context_max"] = 128,
                    ["num_video_frames"] = 25
                }
            };
        }
        long ticks = Environment.TickCount64;
        await API.RunWebsocketHandlerCallWS<object>(async (s, t, a, b) =>
        {
            await ComfyUIBackendExtension.RunArbitraryWorkflowOnFirstBackend(workflow.ToString(), data =>
            {
                if (data is JObject jData && jData.ContainsKey("overall_percent"))
                {
                    long newTicks = Environment.TickCount64;
                    if (newTicks - ticks > 500)
                    {
                        ticks = newTicks;
                        a(new() { ["status"] = $"Running, monitor Server logs for precise progress...\nOverall progress estimate: {jData["overall_percent"]}%" });
                    }
                }
            }, false);
            a(new() { ["status"] = "Process completed, moving engine..." });
            string directory = $"{backend.ComfyPathBase}/output/swarmtemptrt/{prefix}/";
            if (!Directory.Exists(directory))
            {
                a(new() { ["error"] = "Process completed but TensorRT model did not save. Something went wrong?" });
                return;
            }
            string file = Directory.EnumerateFiles(directory, "*.engine").FirstOrDefault();
            if (!File.Exists(file))
            {
                a(new() { ["error"] = "Process completed but TensorRT model did not save. Something went wrong?" });
                return;
            }
            string outPathRaw = $"{Program.ServerSettings.Paths.ModelRoot}/tensorrt/{modelData.Name}_TensorRT";
            string outPath = outPathRaw;
            int id = 0;
            while (File.Exists($"{outPath}.engine"))
            {
                id++;
                outPath = $"{outPathRaw}_{id}";
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            JObject metadata = modelData.ToNetObject();
            metadata["architecture"] += "/tensorrt";
            metadata["title"] = $"{modelData.Title ?? modelData.Name} (TensorRT)";
            File.WriteAllText($"{outPath}.json", metadata.ToString());
            File.Copy(file, $"{outPath}.engine", true);
            File.Delete(file);
            Directory.Delete(directory, true);
            Program.RefreshAllModelSets();
            a(new() { ["status"] = "Complete!", ["complete"] = true });
        }, session, null, ws);
        return null;
    }

    /// <summary>API route to extract a LoRA from two models.</summary>
    public static async Task<JObject> DoLoraExtractionWS(Session session, WebSocket ws, string baseModel, string otherModel, int rank, string outName)
    {
        outName = Utilities.StrictFilenameClean(outName);
        if (ModelsAPI.TryGetRefusalForModel(session, baseModel, out JObject refusal)
            || ModelsAPI.TryGetRefusalForModel(session, otherModel, out refusal)
            || ModelsAPI.TryGetRefusalForModel(session, outName, out refusal))
        {
            await ws.SendJson(refusal, API.WebsocketTimeout);
            return null;
        }
        if (rank < 1 || rank > 320)
        {
            await ws.SendJson(new JObject() { ["error"] = "Rank must be between 1 and 320." }, API.WebsocketTimeout);
            return null;
        }
        baseModel = T2IParamTypes.GetBestModelInList(baseModel, Program.MainSDModels.Models.Keys);
        otherModel = T2IParamTypes.GetBestModelInList(otherModel, Program.MainSDModels.Models.Keys);
        T2IModel baseModelData = Program.MainSDModels.Models.GetValueOrDefault(baseModel);
        T2IModel otherModelData = Program.MainSDModels.Models.GetValueOrDefault(otherModel);
        if (baseModelData is null || otherModelData is null)
        {
            await ws.SendJson(new JObject() { ["error"] = "Unknown input model name." }, API.WebsocketTimeout);
            return null;
        }
        string format = ComfyUIBackendExtension.RunningComfyBackends.FirstOrDefault()?.ModelFolderFormat;
        string arch = otherModelData.ModelClass is null ? "unknown/lora" : $"{otherModelData.ModelClass.ID}/lora";
        JObject metadata = new()
        {
            ["modelspec.architecture"] = arch,
            ["modelspec.title"] = otherModelData.Metadata.Title + " (Extracted LoRA)",
            ["modelspec.description"] = $"LoRA of {otherModelData.Metadata.Title} extracted from {baseModelData.Metadata.Title} at rank {rank}.\n{otherModelData.Metadata.Description}",
            ["modelspec.date"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["modelspec.resolution"] = $"{otherModelData.Metadata.StandardWidth}x{otherModelData.Metadata.StandardHeight}",
            ["modelspec.sai_model_spec"] = "1.0.0"
        };
        if (otherModelData.Metadata.PreviewImage is not null && otherModelData.Metadata.PreviewImage != "imgs/model_placeholder.jpg")
        {
            metadata["modelspec.thumbnail"] = otherModelData.Metadata.PreviewImage;
        }
        JObject workflow = new()
        {
            ["4"] = new JObject()
            {
                ["class_type"] = "CheckpointLoaderSimple",
                ["inputs"] = new JObject()
                {
                    ["ckpt_name"] = baseModelData.ToString(format)
                }
            },
            ["5"] = new JObject()
            {
                ["class_type"] = "CheckpointLoaderSimple",
                ["inputs"] = new JObject()
                {
                    ["ckpt_name"] = otherModelData.ToString(format)
                }
            },
            ["6"] = new JObject()
            {
                ["class_type"] = "SwarmExtractLora",
                ["inputs"] = new JObject()
                {
                    ["base_model"] = new JArray() { "4", 0 },
                    ["base_model_clip"] = new JArray() { "4", 1 },
                    ["other_model"] = new JArray() { "5", 0 },
                    ["other_model_clip"] = new JArray() { "5", 1 },
                    ["rank"] = rank,
                    ["save_rawpath"] = Program.T2IModelSets["LoRA"].FolderPaths[0] + "/",
                    ["save_filename"] = outName.Replace('\\', '/').Replace("/", format ?? $"{Path.DirectorySeparatorChar}"),
                    ["save_clip"] = "true",
                    ["metadata"] = metadata.ToString()
                }
            }
        };
        Logs.Info($"Starting LoRA extraction (for user {session.User.UserID}) for base '{baseModel}', other '{otherModel}', rank {rank}, output to '{outName}'...");
        long ticks = Environment.TickCount64;
        await API.RunWebsocketHandlerCallWS<object>(async (s, t, a, b) =>
        {
            await ComfyUIBackendExtension.RunArbitraryWorkflowOnFirstBackend(workflow.ToString(), data =>
            {
                if (data is JObject jData && jData.ContainsKey("overall_percent"))
                {
                    long newTicks = Environment.TickCount64;
                    if (newTicks - ticks > 500)
                    {
                        ticks = newTicks;
                        a(jData);
                    }
                }
            });
        }, session, null, ws);
        T2IModelHandler loras = Program.T2IModelSets["LoRA"];
        loras.Refresh();
        if (loras.Models.ContainsKey($"{outName}.safetensors"))
        {
            Logs.Info($"Completed successful LoRA extraction for user '{session.User.UserID}' saved as '{outName}'.");
            await ws.SendJson(new JObject() { ["success"] = true }, API.WebsocketTimeout);
            return null;
        }
        else
        {
            Logs.Error($"LoRA extraction FAILED for user '{session.User.UserID}' for target '{outName}' - model did not save.");
            await ws.SendJson(new JObject() { ["error"] = "Extraction failed, lora not saved." }, API.WebsocketTimeout);
            return null;
        }
    }
}
