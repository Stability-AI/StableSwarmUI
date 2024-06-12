using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using StableSwarmUI.WebAPI;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;

namespace StableSwarmUI.Backends;

/// <summary>A backend for Swarm to connect to other Swarm instances to use as the backend.</summary>
public class SwarmSwarmBackend : AbstractT2IBackend
{
    public class SwarmSwarmBackendSettings : AutoConfiguration
    {
        [ConfigComment("The network address of the other Swarm instance.\nUsually starts with 'http://' and ends with ':7801'.")]
        public string Address = "";

        [ConfigComment("Whether the backend is allowed to revert to an 'idle' state if the API address is unresponsive.\nAn idle state is not considered an error, but cannot generate.\nIt will automatically return to 'running' if the API becomes available.")]
        public bool AllowIdle = false;

        [ConfigComment("Whether remote Swarm backends should be followed through.\nIf false, only backends directly local to the remote machine are used.\nIf true, the remote backend can chain further connected backends.")]
        public bool AllowForwarding = true;

        [ConfigComment("Whether the backend is allowed to use WebSocket connections.\nIf true, the backend will work normally and provide previews and updates and all.\nIf false, the backend will freeze while generating until the generation completes.\nFalse may be needed for some limited network environments.")]
        public bool AllowWebsocket = true;
    }

    /// <summary>Internal HTTP handler.</summary>
    public static HttpClient HttpClient = NetworkBackendUtils.MakeHttpClient();

    public SwarmSwarmBackendSettings Settings => SettingsRaw as SwarmSwarmBackendSettings;

    public NetworkBackendUtils.IdleMonitor Idler = new();

    /// <summary>A set of all supported features the remote Swarm instance has.</summary>
    public ConcurrentDictionary<string, string> RemoteFeatureCombo = new();

    /// <summary>A set of all backend-types the remote Swarm instance has.</summary>
    public volatile HashSet<string> RemoteBackendTypes = [];

    /// <inheritdoc/>
    public override IEnumerable<string> SupportedFeatures => RemoteFeatureCombo.Keys;

    /// <summary>Current API session ID.</summary>
    public string Session;

    /// <summary>If true, at least one remote sub-backend is still 'loading'.</summary>
    public volatile bool AnyLoading = true;

    /// <summary>How many sub-backends are available.</summary>
    public volatile int BackendCount = 0;

    /// <summary>The remote backend ID this specific instance is linked to (if any).</summary>
    public int LinkedRemoteBackendID;

    /// <summary>A list of any non-real backends this instance controls.</summary>
    public ConcurrentDictionary<int, BackendHandler.T2IBackendData> ControlledNonrealBackends = new();

    public Dictionary<string, Dictionary<string, JObject>> RemoteModels = null;

    /// <summary>Gets the current target address.</summary>
    public string Address => Settings.Address.TrimEnd('/'); // Remove trailing slash to avoid issues.

    public async Task ValidateAndBuild()
    {
        JObject sessData = await HttpClient.PostJson($"{Address}/API/GetNewSession", []);
        Session = sessData["session_id"].ToString();
        string id = sessData["server_id"]?.ToString();
        BackendCount = sessData["count_running"].Value<int>();
        if (id == Utilities.LoopPreventionID.ToString())
        {
            Logs.Error($"Swarm is connecting to itself as a backend. This is a bad idea. Check the address being used: {Address}");
            throw new Exception("Swarm connected to itself, backend load failed.");
        }
        await ReviseRemoteDataList(true);
    }

    public static void AutoThrowException(JObject data)
    {
        if (data.TryGetValue("error_id", out JToken errorId) && errorId.ToString() == "invalid_session_id")
        {
            throw new SessionInvalidException();
        }
        if (data.TryGetValue("error", out JToken error))
        {
            string err = error.ToString();
            if (err.StartsWith("Invalid data: "))
            {
                throw new InvalidDataException(err["Invalid data: ".Length..]);
            }
            else if (err.StartsWith("Invalid operation: "))
            {
                throw new InvalidOperationException(err["Invalid operation: ".Length..]);
            }
            else
            {
                throw new InvalidDataException($"Remote swarm gave error: {err}");
            }
        }
    }

    public void TriggerRefresh()
    {
        if (!IsReal)
        {
            return;
        }
        _ = RunWithSession(async () =>
        {
            Logs.Verbose($"Trigger refresh on remote swarm {Address}");
            await HttpClient.PostJson($"{Address}/TriggerRefresh", new() { ["session_id"] = Session });
            List<Task> tasks =
            [
                ReviseRemoteDataList(true)
            ];
            foreach (BackendHandler.T2IBackendData backend in ControlledNonrealBackends.Values)
            {
                tasks.Add((backend.Backend as SwarmSwarmBackend).ReviseRemoteDataList(false));
            }
            await Task.WhenAll(tasks);
        });
    }

    public async Task ReviseRemoteDataList(bool fullLoad)
    {
        await RunWithSession(async () =>
        {
            JObject backendData = await HttpClient.PostJson($"{Address}/API/ListBackends", new() { ["session_id"] = Session, ["nonreal"] = true, ["full_data"] = true });
            AutoThrowException(backendData);
            if (IsReal && fullLoad)
            {
                List<Task> tasks = [];
                RemoteModels ??= [];
                foreach (string type in Program.T2IModelSets.Keys)
                {
                    string runType = type;
                    tasks.Add(Task.Run(async () =>
                    {
                        JObject modelsData = await HttpClient.PostJson($"{Address}/API/ListModels", new() { ["session_id"] = Session, ["path"] = "", ["depth"] = 10, ["subtype"] = runType });
                        Dictionary<string, JObject> remoteModelsParsed = [];
                        foreach (JToken x in modelsData["files"].ToList())
                        {
                            JObject data = x.DeepClone() as JObject;
                            data["local"] = false;
                            remoteModelsParsed[data["name"].ToString()] = data;
                        }
                        RemoteModels[runType] = remoteModelsParsed;
                        Models[runType] = [.. remoteModelsParsed.Keys];
                    }));
                }
                await Task.WhenAll(tasks);
            }
            HashSet<string> features = [], types = [];
            bool isLoading = false;
            HashSet<int> ids = IsReal ? new(ControlledNonrealBackends.Keys) : null;
            if (!IsReal)
            {
                if (backendData.TryGetValue($"{LinkedRemoteBackendID}", out JToken data))
                {
                    backendData = new JObject()
                    {
                        [$"{LinkedRemoteBackendID}"] = data
                    };
                }
                else
                {
                    return;
                }
            }
            foreach (JToken backend in backendData.Values())
            {
                string status = backend["status"].ToString();
                int id = backend["id"].Value<int>();
                if (status == "running")
                {
                    features.UnionWith(backend["features"].ToArray().Select(f => f.ToString()));
                    string type = backend["type"].ToString();
                    types.Add(type);
                    if (IsReal && !ids.Remove(id) && (Settings.AllowForwarding || type != "swarmswarmbackend"))
                    {
                        Logs.Verbose($"{HandlerTypeData.Name} {BackendData.ID} adding remote backend {id} ({type})");
                        BackendHandler.T2IBackendData newData = Handler.AddNewNonrealBackend(HandlerTypeData, BackendData, SettingsRaw);
                        SwarmSwarmBackend newSwarm = newData.Backend as SwarmSwarmBackend;
                        newSwarm.LinkedRemoteBackendID = id;
                        newSwarm.Models = Models;
                        newSwarm.CanLoadModels = backend["can_load_models"].Value<bool>();
                        ControlledNonrealBackends.TryAdd(id, newData);
                    }
                    if (ControlledNonrealBackends.TryGetValue(id, out BackendHandler.T2IBackendData data))
                    {
                        data.Backend.MaxUsages = backend["max_usages"].Value<int>();
                        data.Backend.CurrentModelName = (string)backend["current_model"];
                    }
                }
                else if (status == "loading")
                {
                    isLoading = true;
                }
            }
            if (IsReal)
            {
                foreach (int id in ids)
                {
                    Logs.Verbose($"{HandlerTypeData.Name} {BackendData.ID} removing remote backend {id}.");
                    if (ControlledNonrealBackends.Remove(id, out BackendHandler.T2IBackendData data))
                    {
                        await Handler.DeleteById(data.ID);
                    }
                }
            }
            foreach (string str in features.Where(f => !RemoteFeatureCombo.ContainsKey(f)))
            {
                RemoteFeatureCombo.TryAdd(str, str);
            }
            foreach (string str in RemoteFeatureCombo.Keys.Where(f => !features.Contains(f)))
            {
                RemoteFeatureCombo.TryRemove(str, out _);
            }
            AnyLoading = isLoading;
            RemoteBackendTypes = types;
        });
    }

    public class SessionInvalidException : Exception
    {
    }

    public async Task RunWithSession(Func<Task> run)
    {
        try
        {
            await run();
        }
        catch (SessionInvalidException)
        {
            Logs.Verbose($"{HandlerTypeData.Name} {BackendData.ID} session invalid, resetting...");
            await ValidateAndBuild();
            await RunWithSession(run);
        }
    }

    /// <inheritdoc/>
    public override async Task Init()
    {
        if (IsReal)
        {
            CanLoadModels = false;
            Models = [];
        }
        if (string.IsNullOrWhiteSpace(Settings.Address))
        {
            Status = BackendStatus.DISABLED;
            return;
        }
        if (!IsReal)
        {
            Status = BackendStatus.LOADING;
            try
            {
                await ValidateAndBuild();
                Status = BackendStatus.RUNNING;
            }
            catch (Exception ex)
            {
                Logs.Verbose($"{HandlerTypeData.Name} {BackendData.ID} failed to load, WillIdle={Settings.AllowIdle}, Status={Status}: {ex}");
                if (Status != BackendStatus.LOADING)
                {
                    return;
                }
                if (Settings.AllowIdle)
                {
                    Status = BackendStatus.IDLE;
                }
                else
                {
                    Status = BackendStatus.ERRORED;
                    Logs.Error($"Non-real {HandlerTypeData.Name} {BackendData.ID} failed to load: {ex}");
                }
            }
            return;
        }
        Idler.Stop();
        async Task PostEnable()
        {
            if (Settings.AllowIdle)
            {
                Idler.Backend = this;
                Idler.ValidateCall = () => ReviseRemoteDataList(false).Wait();
                Idler.StatusChangeEvent = status =>
                {
                    foreach (BackendHandler.T2IBackendData data in ControlledNonrealBackends.Values)
                    {
                        data.Backend.Status = status;
                    }
                };
                Idler.Start();
            }
        }
        try
        {
            Status = BackendStatus.LOADING;
            await ValidateAndBuild();
            _ = Task.Run(async () =>
            {
                try
                {
                    while (AnyLoading)
                    {
                        Logs.Debug($"{HandlerTypeData.Name} {BackendData.ID} waiting for remote backends to load, have featureset {RemoteFeatureCombo.Keys.JoinString(", ")}");
                        if (Program.GlobalProgramCancel.IsCancellationRequested
                            || Status != BackendStatus.LOADING)
                        {
                            return;
                        }
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        await ReviseRemoteDataList(true);
                    }
                    Status = BackendStatus.RUNNING;
                }
                catch (Exception ex)
                {
                    if (!Settings.AllowIdle)
                    {
                        Logs.Error($"{HandlerTypeData.Name} {BackendData.ID} failed to load: {ex}");
                        Status = BackendStatus.ERRORED;
                        return;
                    }
                }
                await PostEnable();
            });
        }
        catch (Exception)
        {
            if (!Settings.AllowIdle)
            {
                throw;
            }
            await PostEnable();
        }
    }

    /// <inheritdoc/>
    public override async Task Shutdown()
    {
        if (IsReal)
        {
            Logs.Info($"{HandlerTypeData.Name} {BackendData.ID} shutting down...");
            Idler.Stop();
            foreach (BackendHandler.T2IBackendData data in ControlledNonrealBackends.Values)
            {
                await Handler.DeleteById(data.ID);
            }
            ControlledNonrealBackends.Clear();
        }
        Status = BackendStatus.DISABLED;
    }

    /// <inheritdoc/>
    public override async Task<bool> LoadModel(T2IModel model)
    {
        if (IsReal)
        {
            return false;
        }
        bool success = false;
        await RunWithSession(async () =>
        {
            JObject req = new()
            {
                ["session_id"] = Session,
                ["model"] = model.Name,
                ["backendId"] = LinkedRemoteBackendID
            };
            JObject response = await HttpClient.PostJson($"{Address}/API/SelectModel", req);
            AutoThrowException(response);
            success = response.TryGetValue("success", out JToken successTok) && successTok.Value<bool>();
        });
        if (!success)
        {
            Logs.Debug($"{HandlerTypeData.Name} {BackendData.ID} remote backend failed to load model '{model.Name}'.");
            return false;
        }
        CurrentModelName = model.Name;
        return true;
    }

    public JObject BuildRequest(T2IParamInput user_input)
    {
        JObject req = user_input.ToJSON();
        req[T2IParamTypes.Images.Type.ID] = 1;
        req["session_id"] = Session;
        req[T2IParamTypes.DoNotSave.Type.ID] = true;
        req.Remove(T2IParamTypes.ExactBackendID.Type.ID);
        req.Remove(T2IParamTypes.BackendType.Type.ID);
        if (!IsReal)
        {
            req[T2IParamTypes.ExactBackendID.Type.ID] = LinkedRemoteBackendID;
        }
        return req;
    }

    /// <inheritdoc/>
    public override async Task<Image[]> Generate(T2IParamInput user_input)
    {
        user_input.ProcessPromptEmbeds(x => $"<embedding:{x}>");
        Image[] images = null;
        await RunWithSession(async () =>
        {
            JObject generated = await HttpClient.PostJson($"{Address}/API/GenerateText2Image", BuildRequest(user_input));
            AutoThrowException(generated);
            images = generated["images"].Select(img => Image.FromDataString(img.ToString())).ToArray();
        });
        return images;
    }

    /// <inheritdoc/>
    public override async Task GenerateLive(T2IParamInput user_input, string batchId, Action<object> takeOutput)
    {
        if (!Settings.AllowWebsocket)
        {
            Image[] results = await Generate(user_input);
            foreach (Image img in results)
            {
                takeOutput(img);
            }
            return;
        }
        user_input.ProcessPromptEmbeds(x => $"<embedding:{x}>");
        await RunWithSession(async () =>
        {
            ClientWebSocket websocket = await NetworkBackendUtils.ConnectWebsocket(Address, "API/GenerateText2ImageWS");
            await websocket.SendJson(BuildRequest(user_input), API.WebsocketTimeout);
            while (true)
            {
                if (user_input.InterruptToken.IsCancellationRequested)
                {
                    // TODO: This will require separate remote sessions per-user for multiuser support
                    HttpClient.PostJson($"{Address}/API/InterruptAll", new JObject()
                    {
                        ["session_id"] = Session,
                        ["other_sessions"] = false
                    }).Wait();
                }
                JObject response = await websocket.ReceiveJson(1024 * 1024 * 100, true);
                if (response is not null)
                {
                    AutoThrowException(response);
                    if (response.TryGetValue("gen_progress", out JToken val) && val is JObject objVal)
                    {
                        if (objVal.ContainsKey("preview"))
                        {
                            Logs.Verbose($"[{HandlerTypeData.Name}] Got progress image from websocket {batchId}");
                        }
                        else
                        {
                            Logs.Verbose($"[{HandlerTypeData.Name}] Got progress from websocket for {batchId}: {response.ToDenseDebugString(true)}");
                        }
                        string actualId = batchId;
                        if (objVal.TryGetValue("batch_index", out JToken batchIndRemote) && int.TryParse($"{batchIndRemote}", out int batchIndRemoteParsed) && batchIndRemoteParsed > 0 && int.TryParse(batchId, out int localInd))
                        {
                            actualId = $"{localInd + batchIndRemoteParsed}";
                        }
                        objVal["batch_index"] = actualId;
                        takeOutput(objVal);
                    }
                    else if (response.TryGetValue("image", out val))
                    {
                        Logs.Verbose($"[{HandlerTypeData.Name}] Got image from websocket");
                        takeOutput(Image.FromDataString(val.ToString()));
                    }
                    else
                    {
                        Logs.Verbose($"[{HandlerTypeData.Name}] Got other from websocket: {response.ToDenseDebugString(true)}");
                    }
                }
                if (websocket.CloseStatus.HasValue)
                {
                    break;
                }
            }
            await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, Program.GlobalProgramCancel);
        });
    }

    /// <inheritdoc/>
    public override async Task<bool> FreeMemory(bool systemRam)
    {
        if (IsReal)
        {
            return false;
        }
        bool result = false;
        await RunWithSession(async () =>
        {
            JObject response = await HttpClient.PostJson($"{Address}/API/FreeBackendMemory", new() { ["session_id"] = Session, ["system_ram"] = systemRam, ["backend"] = $"{LinkedRemoteBackendID}" });
            AutoThrowException(response);
            result = response["result"].Value<bool>();
        });
        return Volatile.Read(ref result);
    }
}
