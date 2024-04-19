using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Builtin_ComfyUIBackend;
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
        API.RegisterAPICall(ComfySaveWorkflow);
        API.RegisterAPICall(ComfyReadWorkflow);
        API.RegisterAPICall(ComfyListWorkflows);
        API.RegisterAPICall(ComfyDeleteWorkflow);
        API.RegisterAPICall(ComfyGetGeneratedWorkflow);
        API.RegisterAPICall(DoLoraExtractionWS);
    }

    /// <summary>API route to save a comfy workflow object to persistent file.</summary>
    public static async Task<JObject> ComfySaveWorkflow(string name, string workflow, string prompt, string custom_params, string image, string description = "", bool enable_in_simple = false)
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
        if (string.IsNullOrWhiteSpace("image"))
        {
            image = "/imgs/model_placeholder.jpg";
        }
        ComfyUIBackendExtension.CustomWorkflows[cleaned] = new ComfyUIBackendExtension.ComfyCustomWorkflow(cleaned, workflow, prompt, custom_params, image, description, enable_in_simple);
        JObject data = new()
        {
            ["workflow"] = workflow,
            ["prompt"] = prompt,
            ["custom_params"] = custom_params,
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
        ComfyUIBackendExtension.CustomWorkflows.Remove(path, out _);
        path = $"{ComfyUIBackendExtension.Folder}/CustomWorkflows/{path}.json";
        if (!File.Exists(path))
        {
            return new JObject() { ["error"] = "Unknown custom workflow name." };
        }
        File.Delete(path);
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
        string format = ComfyUIBackendExtension.ComfyBackendsDirect().FirstOrDefault().Backend.SupportedFeatures.Contains("folderbackslash") ? "\\" : "/";
        string flow = ComfyUIAPIAbstractBackend.CreateWorkflow(input, w => w, format);
        return new JObject() { ["workflow"] = flow };
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
        T2IModel baseModelData = Program.MainSDModels.Models[baseModel];
        T2IModel otherModelData = Program.MainSDModels.Models[otherModel];
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
                    ["save_rawpath"] = Program.T2IModelSets["LoRA"].FolderPath + "/",
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
