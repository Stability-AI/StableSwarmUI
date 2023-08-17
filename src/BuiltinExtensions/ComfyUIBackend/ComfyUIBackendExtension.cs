using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using StableSwarmUI.WebAPI;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Xml.Linq;

namespace StableSwarmUI.Builtin_ComfyUIBackend;

/// <summary>Main class for the ComfyUI Backend extension.</summary>
public class ComfyUIBackendExtension : Extension
{
    public static string Folder;

    public static Dictionary<string, string> Workflows;

    /// <summary>Set of all feature-ids supported by ComfyUI backends.</summary>
    public static HashSet<string> FeaturesSupported = new() { "comfyui", "refiners", "controlnet" };

    public override void OnPreInit()
    {
        Folder = FilePath;
        LoadWorkflowFiles();
        Program.ModelRefreshEvent += Refresh;
        ScriptFiles.Add("Assets/comfy_workflow_editor_helper.js");
        StyleSheetFiles.Add("Assets/comfy_workflow_editor.css");
        T2IParamTypes.FakeTypeProviders.Add(DynamicParamGenerator);
    }

    public override void OnShutdown()
    {
        T2IParamTypes.FakeTypeProviders.Remove(DynamicParamGenerator);
    }

    public static T2IParamType FakeRawInputType = new("comfyworkflowraw", "", "", Type: T2IParamDataType.TEXT, ID: "comfyworkflowraw", FeatureFlag: "comfyui", HideFromMetadata: true); // TODO: Setting to toggle metadata

    public T2IParamType DynamicParamGenerator(string name, T2IParamInput context)
    {
        if (name == "comfyworkflowraw")
        {
            return FakeRawInputType;
        }
        else if (name.StartsWith("comfyrawworkflowinput") && context.ValuesInput.ContainsKey("comfyworkflowraw"))
        {
            string nameNoPrefix = name.After("comfyrawworkflowinput");
            T2IParamDataType type = FakeRawInputType.Type;
            NumberViewType numberType = NumberViewType.BIG;
            if (nameNoPrefix.StartsWith("seed"))
            {
                type = T2IParamDataType.INTEGER;
                numberType = NumberViewType.SEED;
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
            T2IParamType resType = FakeRawInputType with { Name = nameNoPrefix, ID = name, HideFromMetadata = false, Type = type, NumberView = numberType };
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
        return null;
    }

    public static IEnumerable<ComfyUIAPIAbstractBackend> RunningComfyBackends => Program.Backends.T2IBackends.Values
            .Select(b => b.Backend as ComfyUIAPIAbstractBackend)
            .Where(b => b is not null && b.Status == BackendStatus.RUNNING);

    public void LoadWorkflowFiles()
    {
        Workflows = new();
        foreach (string workflow in Directory.EnumerateFiles($"{Folder}/Workflows", "*.json", new EnumerationOptions() { RecurseSubdirectories = true }).Order())
        {
            string fileName = workflow.Replace('\\', '/').After("/Workflows/");
            if (fileName.EndsWith(".json"))
            {
                Workflows.Add(fileName.BeforeLast('.'), File.ReadAllText(workflow));
            }
        }
        CustomWorkflows.Clear();
        if (Directory.Exists($"{Folder}/CustomWorkflows"))
        {
            foreach (string workflow in Directory.EnumerateFiles($"{Folder}/CustomWorkflows", "*.json", new EnumerationOptions() { RecurseSubdirectories = true }).Order())
            {
                string fileName = workflow.Replace('\\', '/').After("/CustomWorkflows/");
                if (fileName.EndsWith(".json"))
                {
                    string name = fileName.BeforeLast('.');
                    CustomWorkflows.TryAdd(name, name);
                }
            }
        }
    }

    public void Refresh()
    {
        LoadWorkflowFiles();
        List<Task> tasks = new();
        foreach (ComfyUIAPIAbstractBackend backend in RunningComfyBackends.ToArray())
        {
            tasks.Add(backend.LoadValueSet());
        }
        if (tasks.Any())
        {
            Task.WaitAll(tasks.ToArray(), Program.GlobalProgramCancel);
        }
    }


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
            }
        }
    }

    public static LockObject ValueAssignmentLocker = new();

    public static T2IRegisteredParam<string> WorkflowParam, CustomWorkflowParam, SamplerParam, SchedulerParam, RefinerUpscaleMethod, ControlNetPreprocessorParam;

    public static List<string> UpscalerModels = new() { "latent-nearest-exact", "latent-bilinear", "latent-area", "latent-bicubic", "latent-bislerp", "pixel-nearest-exact", "pixel-bilinear", "pixel-area", "pixel-bicubic" },
        Samplers = new() { "euler", "euler_ancestral", "heun", "dpm_2", "dpm_2_ancestral", "lms", "dpm_fast", "dpm_adaptive", "dpmpp_2s_ancestral", "dpmpp_sde", "dpmpp_2m", "dpmpp_2m_sde", "ddim", "uni_pc", "uni_pc_bh2" },
        Schedulers = new() { "normal", "karras", "exponential", "simple", "ddim_uniform" };

    public static ConcurrentDictionary<string, JToken> ControlNetPreprocessors = new() { ["None"] = null };

    /// <summary>All current custom workflow IDs. Values are just a copy of the name (because C# lacks a ConcurrentList).</summary>
    public static ConcurrentDictionary<string, string> CustomWorkflows = new();

    public static T2IParamGroup ComfyGroup, ComfyAdvancedGroup;

    public override void OnInit()
    {
        ComfyGroup = new("ComfyUI", Toggles: false, Open: false);
        ComfyAdvancedGroup = new("ComfyUI Advanced", Toggles: false, IsAdvanced: true, Open: false);
        WorkflowParam = T2IParamTypes.Register<string>(new("[ComfyUI] Workflow", "What hand-written specialty workflow to use in ComfyUI (files in 'Workflows' folder within the ComfyUI extension)",
            "basic", Toggleable: true, FeatureFlag: "comfyui", Group: ComfyAdvancedGroup, IsAdvanced: true, VisibleNormally: false,
            GetValues: (_) => Workflows.Keys.ToList()
            ));
        CustomWorkflowParam = T2IParamTypes.Register<string>(new("[ComfyUI] Custom Workflow", "What custom workflow to use in ComfyUI (built in the Comfy Workflow Editor tab)",
            "", Toggleable: true, FeatureFlag: "comfyui", Group: ComfyGroup, IsAdvanced: true,
            GetValues: (_) => CustomWorkflows.Keys.Order().ToList()
            ));
        SamplerParam = T2IParamTypes.Register<string>(new("[ComfyUI] Sampler", "Sampler type (for ComfyUI)",
            "euler", Toggleable: true, FeatureFlag: "comfyui", Group: ComfyGroup,
            GetValues: (_) => Samplers
            ));
        SchedulerParam = T2IParamTypes.Register<string>(new("[ComfyUI] Scheduler", "Scheduler type (for ComfyUI)",
            "normal", Toggleable: true, FeatureFlag: "comfyui", Group: ComfyGroup,
            GetValues: (_) => Schedulers
            ));
        RefinerUpscaleMethod = T2IParamTypes.Register<string>(new("Refiner Upscale Method", "How to upscale the image, if upscaling is used.",
            "pixel-bilinear", Group: T2IParamTypes.GroupRefiners, OrderPriority: 1, FeatureFlag: "comfyui",
            GetValues: (_) => UpscalerModels
            ));
        ControlNetPreprocessorParam = T2IParamTypes.Register<string>(new("ControlNet Preprocessor", "The preprocessor to use on the ControlNet input image.\nIf toggled off, will be automatically selected.\nUse 'None' to disable preprocessing.",
            "None", Toggleable: true, FeatureFlag: "controlnet", Group: T2IParamTypes.GroupControlNet, OrderPriority: 3, GetValues: (_) => ControlNetPreprocessors.Keys.Order().OrderBy(v => v == "None" ? -1 : 0).ToList()
            ));
        Program.Backends.RegisterBackendType<ComfyUIAPIBackend>("comfyui_api", "ComfyUI API By URL", "A backend powered by a pre-existing installation of ComfyUI, referenced via API base URL.");
        Program.Backends.RegisterBackendType<ComfyUISelfStartBackend>("comfyui_selfstart", "ComfyUI Self-Starting", "A backend powered by a pre-existing installation of the ComfyUI, automatically launched and managed by this UI server.");
        API.RegisterAPICall(ComfySaveWorkflow);
        API.RegisterAPICall(ComfyReadWorkflow);
        API.RegisterAPICall(ComfyListWorkflows);
        API.RegisterAPICall(ComfyDeleteWorkflow);
    }

    /// <summary>API route to save a comfy workflow object to persistent file.</summary>
    public async Task<JObject> ComfySaveWorkflow(string name, string workflow, string prompt, string custom_params)
    {
        string path = Utilities.FilePathForbidden.TrimToNonMatches(name).Replace(".", "");
        CustomWorkflows.TryAdd(path, path);
        Directory.CreateDirectory($"{Folder}/CustomWorkflows");
        path = $"{Folder}/CustomWorkflows/{path}.json";
        JObject data = new()
        {
            ["workflow"] = workflow,
            ["prompt"] = prompt,
            ["custom_params"] = custom_params
        };
        File.WriteAllBytes(path, data.ToString().EncodeUTF8());
        return new JObject() { ["success"] = true };
    }

    /// <summary>API route to read a comfy workflow object from persistent file.</summary>
    public async Task<JObject> ComfyReadWorkflow(string name)
    {
        string path = Utilities.FilePathForbidden.TrimToNonMatches(name).Replace(".", "");
        path = $"{Folder}/CustomWorkflows/{path}.json";
        if (!File.Exists(path))
        {
            return new JObject() { ["error"] = "Unknown custom workflow name." };
        }
        string data = Encoding.UTF8.GetString(File.ReadAllBytes(path));
        JObject parsed = data.ParseToJson();
        return new JObject() { ["result"] = parsed };
    }

    /// <summary>API route to read a list of available Comfy custom workflows.</summary>
    public async Task<JObject> ComfyListWorkflows()
    {
        return new JObject() { ["workflows"] = JToken.FromObject(CustomWorkflows.Keys.Order().ToList()) };
    }

    /// <summary>API route to read a delete a saved Comfy custom workflows.</summary>
    public async Task<JObject> ComfyDeleteWorkflow(string name)
    {
        string path = Utilities.FilePathForbidden.TrimToNonMatches(name).Replace(".", "");
        CustomWorkflows.Remove(path, out _);
        path = $"{Folder}/CustomWorkflows/{path}.json";
        if (!File.Exists(path))
        {
            return new JObject() { ["error"] = "Unknown custom workflow name." };
        }
        File.Delete(path);
        return new JObject() { ["success"] = true };
    }

    public override void OnPreLaunch()
    {
        WebServer.WebApp.Map("/ComfyBackendDirect/{*Path}", ComfyBackendDirectHandler);
    }

    /// <summary>Web route for viewing output images. This just works as a simple proxy.</summary>
    public async Task ComfyBackendDirectHandler(HttpContext context)
    {
        if (context.Response.StatusCode == 404)
        {
            return;
        }
        ComfyUIAPIAbstractBackend backend = RunningComfyBackends.FirstOrDefault();
        if (backend is null)
        {
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("<!DOCTYPE html><html><head><stylesheet>body{background-color:#101010;color:#eeeeee;}</stylesheet></head><body><span class=\"comfy-failed-to-load\">No ComfyUI backend available, loading failed.</span></body></html>");
            await context.Response.CompleteAsync();
            return;
        }
        string path = context.Request.Path.Value;
        path = path.After("/ComfyBackendDirect");
        if (path.StartsWith("/"))
        {
            path = path[1..];
        }
        if (!string.IsNullOrWhiteSpace(context.Request.QueryString.Value))
        {
            path = $"{path}{context.Request.QueryString.Value}";
        }
        if (context.WebSockets.IsWebSocketRequest)
        {
            WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
            ClientWebSocket outSocket = new();
            outSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            string scheme = backend.Address.BeforeAndAfter("://", out string addr);
            scheme = scheme == "http" ? "ws" : "wss";
            await outSocket.ConnectAsync(new Uri($"{scheme}://{addr}/{path}"), Program.GlobalProgramCancel);
            Task a = Task.Run(async () =>
            {
                try
                {
                    byte[] recvBuf = new byte[10 * 1024 * 1024];
                    while (true)
                    {
                        // TODO: Should this input be allowed to remain open forever? Need a timeout, but the ComfyUI websocket doesn't seem to keepalive properly.
                        WebSocketReceiveResult received = await socket.ReceiveAsync(recvBuf, Program.GlobalProgramCancel);
                        if (received.MessageType != WebSocketMessageType.Close)
                        {
                            await outSocket.SendAsync(recvBuf.AsMemory(0, received.Count), received.MessageType, received.EndOfMessage, Program.GlobalProgramCancel);
                        }
                        if (socket.CloseStatus.HasValue)
                        {
                            await outSocket.CloseAsync(socket.CloseStatus.Value, socket.CloseStatusDescription, Program.GlobalProgramCancel);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logs.Debug($"ComfyUI redirection failed: {ex}");
                }
            });
            Task b = Task.Run(async () =>
            {
                try
                {
                    byte[] recvBuf = new byte[10 * 1024 * 1024];
                    while (true)
                    {
                        WebSocketReceiveResult received = await outSocket.ReceiveAsync(recvBuf, Program.GlobalProgramCancel);
                        if (received.MessageType != WebSocketMessageType.Close)
                        {
                            await socket.SendAsync(recvBuf.AsMemory(0, received.Count), received.MessageType, received.EndOfMessage, Program.GlobalProgramCancel);
                        }
                        if (socket.CloseStatus.HasValue)
                        {
                            await socket.CloseAsync(socket.CloseStatus.Value, socket.CloseStatusDescription, Program.GlobalProgramCancel);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logs.Debug($"ComfyUI redirection failed: {ex}");
                }
            });
            await Task.WhenAll(a, b);
            return;
        }
        // This code is utterly silly, but it's incredibly fragile, don't touch without significant testing
        HttpResponseMessage response;
        if (context.Request.Method == "POST")
        {
            HttpRequestMessage request = new(new HttpMethod("POST"), $"{backend.Address}/{path}") { Content = new StreamContent(context.Request.Body) };
            request.Content.Headers.Add("Content-Type", context.Request.ContentType);
            response = await backend.HttpClient.SendAsync(request);
        }
        else
        {
            response = await backend.HttpClient.SendAsync(new(new(context.Request.Method), $"{backend.Address}/{path}"));
        }
        int code = (int)response.StatusCode;
        if (code != 200)
        {
            Logs.Debug($"ComfyUI redirection gave non-200 code: '{code}' for URL: {context.Request.Method} '{path}'");
        }
        Logs.Verbose($"Comfy Redir status code {code} from {context.Response.StatusCode} and type {response.Content.Headers.ContentType} for {context.Request.Method} '{path}'");
        context.Response.StatusCode = code;
        context.Response.ContentType = response.Content.Headers.ContentType.ToString();
        await response.Content.CopyToAsync(context.Response.Body);
        await context.Response.CompleteAsync();
    }
}
