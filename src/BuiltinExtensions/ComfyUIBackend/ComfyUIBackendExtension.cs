using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
    public static HashSet<string> FeaturesSupported = new() { "comfyui", "refiners", "controlnet", "endstepsearly", "seamless" };

    /// <summary>Extensible map of ComfyUI Node IDs to supported feature IDs.</summary>
    public static Dictionary<string, string> NodeToFeatureMap = new()
    {
        ["SwarmLoadImageB64"] = "comfy_loadimage_b64",
        ["SwarmSaveImageWS"] = "comfy_saveimage_ws",
        ["SwarmKSampler"] = "variation_seed",
        ["FreeU"] = "freeu",
        ["AITemplateLoader"] = "aitemplate",
        ["IPAdapter"] = "ipadapter"
    };

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
        else if (name.StartsWith("comfyrawworkflowinput") && (context.ValuesInput.ContainsKey("comfyworkflowraw") || context.ValuesInput.ContainsKey("comfyuicustomworkflow")))
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
            if (rawObjectInfo.TryGetValue("IPAdapter", out JToken ipadapter))
            {
                IPAdapterModels = IPAdapterModels.Concat(ipadapter["input"]["required"]["model_name"][0].Select(m => $"{m}")).Distinct().ToList();
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
                }
            }
        }
    }

    public static LockObject ValueAssignmentLocker = new();

    public static T2IRegisteredParam<string> WorkflowParam, CustomWorkflowParam, SamplerParam, SchedulerParam, RefinerUpscaleMethod, ControlNetPreprocessorParam, UseIPAdapterForRevision;

    public static T2IRegisteredParam<bool> AITemplateParam, DebugRegionalPrompting;

    public static T2IRegisteredParam<double> IPAdapterWeight;

    public static List<string> UpscalerModels = new() { "latent-nearest-exact", "latent-bilinear", "latent-area", "latent-bicubic", "latent-bislerp", "pixel-nearest-exact", "pixel-bilinear", "pixel-area", "pixel-bicubic" },
        Samplers = new() { "euler", "euler_ancestral", "heun", "dpm_2", "dpm_2_ancestral", "lms", "dpm_fast", "dpm_adaptive", "dpmpp_2s_ancestral", "dpmpp_sde", "dpmpp_2m", "dpmpp_2m_sde", "ddim", "uni_pc", "uni_pc_bh2" },
        Schedulers = new() { "normal", "karras", "exponential", "simple", "ddim_uniform" };

    public static List<string> IPAdapterModels = new() { "None" };

    public static ConcurrentDictionary<string, JToken> ControlNetPreprocessors = new() { ["None"] = null };

    /// <summary>All current custom workflow IDs. Values are just a copy of the name (because C# lacks a ConcurrentList).</summary>
    public static ConcurrentDictionary<string, string> CustomWorkflows = new();

    public static T2IParamGroup ComfyGroup, ComfyAdvancedGroup;

    public override void OnInit()
    {
        UseIPAdapterForRevision = T2IParamTypes.Register<string>(new("Use IP-Adapter", "Use IP-Adapter for ReVision input handling.",
            "None", IgnoreIf: "None", FeatureFlag: "ipadapter", GetValues: _ => IPAdapterModels, Group: T2IParamTypes.GroupRevision, OrderPriority: 15
            ));
        IPAdapterWeight = T2IParamTypes.Register<double>(new("IP-Adapter Weight", "Weight to use with IP-Adapter (if enabled).",
            "1", Min: -1, Max: 3, Step: 0.05, IgnoreIf: "1", FeatureFlag: "ipadapter", Group: T2IParamTypes.GroupRevision, ViewType: ParamViewType.SLIDER, OrderPriority: 16
            ));
        ComfyGroup = new("ComfyUI", Toggles: false, Open: false);
        ComfyAdvancedGroup = new("ComfyUI Advanced", Toggles: false, IsAdvanced: true, Open: false);
        WorkflowParam = T2IParamTypes.Register<string>(new("[ComfyUI] Workflow", "What hand-written specialty workflow to use in ComfyUI (files in 'Workflows' folder within the ComfyUI extension)",
            "basic", Toggleable: true, FeatureFlag: "comfyui", Group: ComfyAdvancedGroup, IsAdvanced: true, VisibleNormally: false,
            GetValues: (_) => Workflows.Keys.ToList()
            ));
        CustomWorkflowParam = T2IParamTypes.Register<string>(new("[ComfyUI] Custom Workflow", "What custom workflow to use in ComfyUI (built in the Comfy Workflow Editor tab)",
            "", Toggleable: true, FeatureFlag: "comfyui", Group: ComfyGroup, IsAdvanced: true, ValidateValues: false,
            GetValues: (_) => CustomWorkflows.Keys.Order().ToList(),
            Clean: ((_, val) => CustomWorkflows.ContainsKey(val) ? $"PARSED%{val}%{ReadCustomWorkflow(val)["prompt"]}" : val),
            MetadataFormat: v => v.StartsWith("PARSED%") ? v.After("%").Before("%") : v
            ));
        SamplerParam = T2IParamTypes.Register<string>(new("[ComfyUI] Sampler", "Sampler type (for ComfyUI)",
            "euler", Toggleable: true, FeatureFlag: "comfyui", Group: ComfyGroup,
            GetValues: (_) => Samplers
            ));
        SchedulerParam = T2IParamTypes.Register<string>(new("[ComfyUI] Scheduler", "Scheduler type (for ComfyUI)",
            "normal", Toggleable: true, FeatureFlag: "comfyui", Group: ComfyGroup,
            GetValues: (_) => Schedulers
            ));
        AITemplateParam = T2IParamTypes.Register<bool>(new("Enable AITemplate", "If checked, enables AITemplate for ComfyUI generations (UNet only). Only compatible with some GPUs.",
            "false", IgnoreIf: "false", FeatureFlag: "aitemplate", Group: ComfyGroup
            ));
        RefinerUpscaleMethod = T2IParamTypes.Register<string>(new("Refiner Upscale Method", "How to upscale the image, if upscaling is used.",
            "pixel-bilinear", Group: T2IParamTypes.GroupRefiners, OrderPriority: 1, FeatureFlag: "comfyui",
            GetValues: (_) => UpscalerModels
            ));
        ControlNetPreprocessorParam = T2IParamTypes.Register<string>(new("ControlNet Preprocessor", "The preprocessor to use on the ControlNet input image.\nIf toggled off, will be automatically selected.\nUse 'None' to disable preprocessing.",
            "None", Toggleable: true, FeatureFlag: "controlnet", Group: T2IParamTypes.GroupControlNet, OrderPriority: 3, GetValues: (_) => ControlNetPreprocessors.Keys.Order().OrderBy(v => v == "None" ? -1 : 0).ToList()
            ));
        DebugRegionalPrompting = T2IParamTypes.Register<bool>(new("Debug Regional Prompting", "If checked, outputs masks from regional prompting for debug reasons.",
            "false", IgnoreIf: "false", FeatureFlag: "comfyui", VisibleNormally: false
            ));
        Program.Backends.RegisterBackendType<ComfyUIAPIBackend>("comfyui_api", "ComfyUI API By URL", "A backend powered by a pre-existing installation of ComfyUI, referenced via API base URL.", true);
        Program.Backends.RegisterBackendType<ComfyUISelfStartBackend>("comfyui_selfstart", "ComfyUI Self-Starting", "A backend powered by a pre-existing installation of the ComfyUI, automatically launched and managed by this UI server.");
        API.RegisterAPICall(ComfySaveWorkflow);
        API.RegisterAPICall(ComfyReadWorkflow);
        API.RegisterAPICall(ComfyListWorkflows);
        API.RegisterAPICall(ComfyDeleteWorkflow);
    }

    /// <summary>API route to save a comfy workflow object to persistent file.</summary>
    public async Task<JObject> ComfySaveWorkflow(string name, string workflow, string prompt, string custom_params)
    {
        string path = Utilities.StrictFilenameClean(name);
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

    /// <summary>Method to directly read a custom workflow file.</summary>
    public static JObject ReadCustomWorkflow(string name)
    {
        string path = Utilities.StrictFilenameClean(name);
        path = $"{Folder}/CustomWorkflows/{path}.json";
        if (!File.Exists(path))
        {
            return new JObject() { ["error"] = "Unknown custom workflow name." };
        }
        string data = Encoding.UTF8.GetString(File.ReadAllBytes(path));
        return data.ParseToJson();
    }

    /// <summary>API route to read a comfy workflow object from persistent file.</summary>
    public async Task<JObject> ComfyReadWorkflow(string name)
    {
        JObject val = ReadCustomWorkflow(name);
        if (val.ContainsKey("error"))
        {
            return val;
        }
        return new JObject() { ["result"] = val };
    }

    /// <summary>API route to read a list of available Comfy custom workflows.</summary>
    public async Task<JObject> ComfyListWorkflows()
    {
        return new JObject() { ["workflows"] = JToken.FromObject(CustomWorkflows.Keys.Order().ToList()) };
    }

    /// <summary>API route to read a delete a saved Comfy custom workflows.</summary>
    public async Task<JObject> ComfyDeleteWorkflow(string name)
    {
        string path = Utilities.StrictFilenameClean(name);
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
        HttpClient client;
        string address;
        ComfyUIAPIAbstractBackend backend = RunningComfyBackends.FirstOrDefault();
        if (backend is null)
        {
            SwarmSwarmBackend altBack = Program.Backends.T2IBackends.Values.Select(b => b.Backend as SwarmSwarmBackend)
                .Where(b => b is not null && b.Status == BackendStatus.RUNNING && b.RemoteBackendTypes.Any(b => b.StartsWith("comfyui_"))).FirstOrDefault();
            if (altBack is not null)
            {
                client = altBack.HttpClient;
                address = $"{altBack.Settings.Address}/ComfyBackendDirect";
            }
            else
            {
                context.Response.ContentType = "text/html";
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("<!DOCTYPE html><html><head><stylesheet>body{background-color:#101010;color:#eeeeee;}</stylesheet></head><body><span class=\"comfy-failed-to-load\">No ComfyUI backend available, loading failed.</span></body></html>");
                await context.Response.CompleteAsync();
                return;
            }
        }
        else
        {
            client = backend.HttpClient;
            address = backend.Address;
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
            string scheme = address.BeforeAndAfter("://", out string addr);
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
            HttpRequestMessage request = new(new HttpMethod("POST"), $"{address}/{path}") { Content = new StreamContent(context.Request.Body) };
            request.Content.Headers.Add("Content-Type", context.Request.ContentType);
            response = await client.SendAsync(request);
        }
        else
        {
            response = await client.SendAsync(new(new(context.Request.Method), $"{address}/{path}"));
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
