using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Builtin_ComfyUIBackend;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using StableSwarmUI.WebAPI;
using System.IO;

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
    }

    /// <summary>API route to save a comfy workflow object to persistent file.</summary>
    public static async Task<JObject> ComfySaveWorkflow(string name, string workflow, string prompt, string custom_params)
    {
        string path = Utilities.StrictFilenameClean(name);
        ComfyUIBackendExtension.CustomWorkflows.TryAdd(path, path);
        Directory.CreateDirectory($"{ComfyUIBackendExtension.Folder}/CustomWorkflows");
        path = $"{ComfyUIBackendExtension.Folder}/CustomWorkflows/{path}.json";
        JObject data = new()
        {
            ["workflow"] = workflow,
            ["prompt"] = prompt,
            ["custom_params"] = custom_params
        };
        File.WriteAllBytes(path, data.ToString().EncodeUTF8());
        return new JObject() { ["success"] = true };
    }

    /// <summary>Method to directly read a custom workflow file.</summary>
    public static JObject ReadCustomWorkflow(string name)
    {
        string path = Utilities.StrictFilenameClean(name);
        path = $"{ComfyUIBackendExtension.Folder}/CustomWorkflows/{path}.json";
        if (!File.Exists(path))
        {
            return new JObject() { ["error"] = "Unknown custom workflow name." };
        }
        string data = Encoding.UTF8.GetString(File.ReadAllBytes(path));
        return data.ParseToJson();
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
        return new JObject() { ["workflows"] = JToken.FromObject(ComfyUIBackendExtension.CustomWorkflows.Keys.Order().ToList()) };
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
}
