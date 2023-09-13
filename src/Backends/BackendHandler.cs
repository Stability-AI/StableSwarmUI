using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.DataHolders;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.IO;
using System.Net.Http;
using System.Reflection;

namespace StableSwarmUI.Backends;

/// <summary>Central manager for available backends.</summary>
public class BackendHandler
{
    /// <summary>Currently loaded backends. Might not all be valid.</summary>
    public ConcurrentDictionary<int, T2IBackendData> T2IBackends = new();

    /// <summary>Signal when any backends are available, or other reason to check backends (eg new requests came in).</summary>
    public AsyncAutoResetEvent CheckBackendsSignal = new(false);

    /// <summary>Central locker to prevent issues with backend validating.</summary>
    public LockObject CentralLock = new();

    /// <summary>Map of backend type IDs to metadata about them.</summary>
    public Dictionary<string, BackendType> BackendTypes = new();

    /// <summary>Value to ensure unique IDs are given to new backends.</summary>
    public int LastBackendID = 0;

    /// <summary>Value to ensure unique IDs are given to new non-real backends.</summary>
    public int LastNonrealBackendID = -1;

    /// <summary>If true, then at some point backends were edited, and re-saving is needed.</summary>
    public bool BackendsEdited = false;

    /// <summary>The path to where the backend list is saved.</summary>
    public string SaveFilePath = "Data/Backends.fds";

    /// <summary>Queue of backends to initialize.</summary>
    public ConcurrentQueue<T2IBackendData> BackendsToInit = new();

    /// <summary>Signal for when a new backend is added to <see cref="BackendsToInit"/>.</summary>
    public AsyncAutoResetEvent NewBackendInitSignal = new(false);

    /// <summary>The number of currently loaded backends.</summary>
    public int Count => T2IBackends.Count;

    public BackendHandler()
    {
        RegisterBackendType<SwarmSwarmBackend>("swarmswarmbackend", "Swarm-API-Backend", "Connection StableSwarmUI to another instance of StableSwarmUI as a backend.");
    }

    /// <summary>Metadata about backend types.</summary>
    public record class BackendType(string ID, string Name, string Description, Type SettingsClass, AutoConfiguration.Internal.AutoConfigData SettingsInternal, Type BackendClass, JObject NetDescription);

    /// <summary>Mapping of C# types to network type labels.</summary>
    public static Dictionary<Type, string> NetTypeLabels = new()
    {
        [typeof(string)] = "text",
        [typeof(int)] = "integer",
        [typeof(long)] = "integer",
        [typeof(float)] = "decimal",
        [typeof(double)] = "decimal",
        [typeof(bool)] = "bool"
    };

    /// <summary>Register a new backend-type by type ref.</summary>
    public BackendType RegisterBackendType(Type type, string id, string name, string description)
    {
        Type settingsType = type.GetNestedTypes().First(t => t.IsSubclassOf(typeof(AutoConfiguration)));
        AutoConfiguration.Internal.AutoConfigData settingsInternal = (Activator.CreateInstance(settingsType) as AutoConfiguration).InternalData.SharedData;
        List<JObject> fields = settingsInternal.Fields.Values.Select(f =>
        {
            return new JObject()
            {
                ["name"] = f.Name,
                ["type"] = NetTypeLabels[f.Field.FieldType],
                ["description"] = f.Field.GetCustomAttribute<AutoConfiguration.ConfigComment>()?.Comments?.ToString() ?? "",
                ["placeholder"] = f.Field.GetCustomAttribute<SuggestionPlaceholder>()?.Text ?? ""
            };
        }).ToList();
        JObject netDesc = new()
        {
            ["id"] = id,
            ["name"] = name,
            ["description"] = description,
            ["settings"] = JToken.FromObject(fields)
        };
        BackendType typeObj = new(id, name, description, settingsType, settingsInternal, type, netDesc);
        BackendTypes.Add(id, typeObj);
        return typeObj;
    }

    /// <summary>Register a new backend-type by type ref.</summary>
    public BackendType RegisterBackendType<T>(string id, string name, string description) where T : AbstractT2IBackend
    {
        return RegisterBackendType(typeof(T), id, name, description);
    }

    /// <summary>Special live data about a registered backend.</summary>
    public class T2IBackendData
    {
        public AbstractT2IBackend Backend;

        public volatile bool IsInUse = false;

        public bool CheckIsInUse => IsInUse && Backend.Status == BackendStatus.RUNNING;

        public LockObject AccessLock = new();

        public int ID;

        public int InitAttempts = 0;

        public int ModCount = 0;

        public long TimeLastRelease;

        public void Claim()
        {
            IsInUse = true;
            TimeLastRelease = Environment.TickCount64;
        }
    }

    /// <summary>Adds a new backend of the given type, and returns its data. Note that the backend will not be initialized at first.</summary>
    public T2IBackendData AddNewOfType(BackendType type, AutoConfiguration config = null)
    {
        BackendsEdited = true;
        T2IBackendData data = new()
        {
            Backend = Activator.CreateInstance(type.BackendClass) as AbstractT2IBackend
        };
        data.Backend.BackendData = data;
        data.Backend.SettingsRaw = config ?? (Activator.CreateInstance(type.SettingsClass) as AutoConfiguration);
        data.Backend.HandlerTypeData = type;
        data.Backend.Handler = this;
        lock (CentralLock)
        {
            data.ID = LastBackendID++;
            T2IBackends.TryAdd(data.ID, data);
        }
        data.Backend.Status = BackendStatus.WAITING;
        BackendsToInit.Enqueue(data);
        NewBackendInitSignal.Set();
        return data;
    }

    /// <summary>Adds a new backend that is not a 'real' backend (it will not save nor show in the UI, but is available for generation calls).</summary>
    public T2IBackendData AddNewNonrealBackend(BackendType type, AutoConfiguration config = null)
    {
        T2IBackendData data = new()
        {
            Backend = Activator.CreateInstance(type.BackendClass) as AbstractT2IBackend
        };
        data.Backend.BackendData = data;
        data.Backend.SettingsRaw = config ?? (Activator.CreateInstance(type.SettingsClass) as AutoConfiguration);
        data.Backend.HandlerTypeData = type;
        data.Backend.Handler = this;
        data.Backend.IsReal = false;
        lock (CentralLock)
        {
            data.ID = LastNonrealBackendID--;
            T2IBackends.TryAdd(data.ID, data);
        }
        data.Backend.Status = BackendStatus.WAITING;
        BackendsToInit.Enqueue(data);
        NewBackendInitSignal.Set();
        return data;
    }

    /// <summary>Shuts down the given backend properly and cleanly, in a way that avoids interrupting usage of the backend.</summary>
    public async Task ShutdownBackendCleanly(T2IBackendData data)
    {
        data.Backend.Reserved = true;
        try
        {
            while (data.CheckIsInUse)
            {
                if (Program.GlobalProgramCancel.IsCancellationRequested)
                {
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(0.5));
            }
            await data.Backend.DoShutdownNow();
        }
        finally
        {
            data.Backend.Reserved = false;
        }
    }

    /// <summary>Shutdown and delete a given backend.</summary>
    public async Task<bool> DeleteById(int id)
    {
        BackendsEdited = true;
        if (!T2IBackends.TryRemove(id, out T2IBackendData data))
        {
            return false;
        }
        await ShutdownBackendCleanly(data);
        ReassignLoadedModelsList();
        return true;
    }

    /// <summary>Replace the settings of a given backend. Shuts it down immediately and queues a reload.</summary>
    public async Task<T2IBackendData> EditById(int id, FDSSection newSettings, string title)
    {
        if (!T2IBackends.TryGetValue(id, out T2IBackendData data))
        {
            return null;
        }
        await ShutdownBackendCleanly(data);
        data.Backend.SettingsRaw.Load(newSettings);
        if (title is not null)
        {
            data.Backend.Title = title;
        }
        BackendsEdited = true;
        data.ModCount++;
        data.Backend.Status = BackendStatus.WAITING;
        BackendsToInit.Enqueue(data);
        return data;
    }

    /// <summary>Causes all backends to restart.</summary>
    public async Task ReloadAllBackends()
    {
        foreach (T2IBackendData data in T2IBackends.Values.ToArray())
        {
            await ShutdownBackendCleanly(data);
            data.Backend.Status = BackendStatus.WAITING;
            BackendsToInit.Enqueue(data);
        }
    }

    /// <summary>Loads the backends list from a file.</summary>
    public void Load()
    {
        if (T2IBackends.Any()) // Backup to prevent duplicate calls
        {
            return;
        }
        LoadInternal();
        NewBackendInitSignal.Set();
        ReassignLoadedModelsList();
        new Thread(new ThreadStart(RequestHandlingLoop)).Start();
    }

    /// <summary>Internal route for loading backends. Do not call directly.</summary>
    public void LoadInternal()
    {
        Logs.Init("Loading backends from file...");
        new Thread(InternalInitMonitor) { Name = "BackendHandler_Init_Monitor" }.Start();
        FDSSection file;
        try
        {
            file = FDSUtility.ReadFile(SaveFilePath);
        }
        catch (Exception ex)
        {
            if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return;
            }
            Console.WriteLine($"Could not read Backends save file: {ex}");
            return;
        }
        if (file is null)
        {
            return;
        }
        foreach (string idstr in file.GetRootKeys())
        {
            FDSSection section = file.GetSection(idstr);
            if (!BackendTypes.TryGetValue(section.GetString("type"), out BackendType type))
            {
                Console.WriteLine($"Unknown backend type '{section.GetString("type")}' in save file, skipping backend #{idstr}.");
                continue;
            }
            T2IBackendData data = new()
            {
                Backend = Activator.CreateInstance(type.BackendClass) as AbstractT2IBackend,
                ID = int.Parse(idstr)
            };
            data.Backend.BackendData = data;
            LastBackendID = Math.Max(LastBackendID, data.ID + 1);
            data.Backend.SettingsRaw = Activator.CreateInstance(type.SettingsClass) as AutoConfiguration;
            data.Backend.SettingsRaw.Load(section.GetSection("settings"));
            data.Backend.IsEnabled = section.GetBool("enabled", true).Value;
            data.Backend.Title = section.GetString("title", "");
            data.Backend.HandlerTypeData = type;
            data.Backend.Handler = this;
            data.Backend.Status = BackendStatus.WAITING;
            BackendsToInit.Enqueue(data);
            lock (CentralLock)
            {
                T2IBackends.TryAdd(data.ID, data);
            }
        }
    }

    /// <summary>Internal thread path for processing new backend initializations.</summary>
    public void InternalInitMonitor()
    {
        while (!HasShutdown)
        {
            bool any = false;
            while (BackendsToInit.TryDequeue(out T2IBackendData data) && !HasShutdown)
            {
                if (!data.Backend.IsEnabled)
                {
                    data.Backend.Status = BackendStatus.DISABLED;
                    continue;
                }
                try
                {
                    if (data.Backend.IsReal)
                    {
                        Logs.Init($"Initializing backend #{data.ID} - {data.Backend.HandlerTypeData.Name}...");
                    }
                    else
                    {
                        Logs.Verbose($"Initializing non-real backend #{data.ID} - {data.Backend.HandlerTypeData.Name}...");
                    }
                    data.InitAttempts++;
                    data.Backend.Init().Wait(Program.GlobalProgramCancel);
                    any = true;
                }
                catch (Exception ex)
                {
                    if (data.InitAttempts <= Program.ServerSettings.Backends.MaxBackendInitAttempts)
                    {
                        data.Backend.Status = BackendStatus.WAITING;
                        Logs.Error($"Error #{data.InitAttempts} while initializing backend #{data.ID} - {data.Backend.HandlerTypeData.Name} - will retry");
                        BackendsToInit.Enqueue(data);
                        Thread.Sleep(TimeSpan.FromSeconds(1)); // Intentionally pause a second to give a chance for external issue to self-resolve.
                    }
                    else
                    {
                        data.Backend.Status = BackendStatus.ERRORED;
                        if (ex is AggregateException aex)
                        {
                            ex = aex.InnerException;
                        }
                        string errorMessage = $"{ex}";
                        if (ex is HttpRequestException hrex && hrex.Message.StartsWith("No connection could be made because the target machine actively refused it"))
                        {
                            errorMessage = $"Connection refused - is the backend running, or is the address correct? (HttpRequestException: {ex.Message})";
                        }
                        Logs.Error($"Final error ({data.InitAttempts}) while initializing backend #{data.ID} - {data.Backend.HandlerTypeData.Name}, giving up: {errorMessage}");
                    }
                }
            }
            if (any)
            {
                try
                {
                    ReassignLoadedModelsList();
                }
                catch (Exception ex)
                {
                    Logs.Error($"Error while reassigning loaded models list: {ex}");
                }
            }
            NewBackendInitSignal.WaitAsync(TimeSpan.FromSeconds(2)).Wait();
        }
    }

    /// <summary>Updates what model(s) are currently loaded.</summary>
    public void ReassignLoadedModelsList()
    {
        foreach (T2IModel model in Program.MainSDModels.Models.Values)
        {
            model.AnyBackendsHaveLoaded = false;
        }
        foreach (T2IBackendData backend in T2IBackends.Values)
        {
            if (backend.Backend.CurrentModelName is not null && Program.MainSDModels.Models.TryGetValue(backend.Backend.CurrentModelName, out T2IModel model))
            {
                model.AnyBackendsHaveLoaded = true;
            }
        }
    }

    /// <summary>Save the backends list to a file.</summary>
    public void Save()
    {
        Logs.Info("Saving backends...");
        FDSSection saveFile = new();
        foreach (T2IBackendData data in T2IBackends.Values)
        {
            if (!data.Backend.IsReal)
            {
                continue;
            }
            FDSSection data_section = new();
            data_section.Set("type", data.Backend.HandlerTypeData.ID);
            data_section.Set("title", data.Backend.Title);
            data_section.Set("enabled", data.Backend.IsEnabled);
            data_section.Set("settings", data.Backend.SettingsRaw.Save(true));
            saveFile.Set(data.ID.ToString(), data_section);
        }
        FDSUtility.SaveToFile(saveFile, SaveFilePath);
    }

    /// <summary>Tells all backends to load a given model. Returns true if any backends have loaded it, or false if not.</summary>
    public async Task<bool> LoadModelOnAll(T2IModel model)
    {
        bool result = await Task.Run(async () => // TODO: this is weird async jank
        {
            bool any = false;
            foreach (T2IBackendData backend in T2IBackends.Values.Where(b => b.Backend.Status == BackendStatus.RUNNING))
            {
                lock (CentralLock)
                {
                    while (backend.CheckIsInUse)
                    {
                        if (Program.GlobalProgramCancel.IsCancellationRequested)
                        {
                            return false;
                        }
                        Thread.Sleep(100);
                    }
                    backend.IsInUse = true;
                }
                try
                {
                    any = (await backend.Backend.LoadModel(model)) || any;
                }
                catch (Exception ex)
                {
                    Logs.Error($"Error loading model on backend {backend.ID} ({backend.Backend.HandlerTypeData.Name}): {ex}");
                }
                backend.IsInUse = false;
            }
            return any;
        });
        ReassignLoadedModelsList();
        return result;
    }

    private volatile bool HasShutdown;

    /// <summary>Main shutdown handler, triggered by <see cref="Program.Shutdown"/>.</summary>
    public void Shutdown()
    {
        if (HasShutdown)
        {
            return;
        }
        HasShutdown = true;
        NewBackendInitSignal.Set();
        CheckBackendsSignal.Set();
        List<(T2IBackendData, Task)> tasks = new();
        foreach (T2IBackendData backend in T2IBackends.Values)
        {
            tasks.Add((backend, Task.Run(async () =>
            {
                int backTicks = 0;
                while (backend.CheckIsInUse)
                {
                    if (backTicks++ > 50)
                    {
                        Logs.Info($"Backend {backend.ID} ({backend.Backend.HandlerTypeData.Name}) has been locked in use for at least 5 seconds after shutdown, giving up and killing anyway.");
                        break;
                    }
                    Thread.Sleep(100);
                }
                tasks.Add((backend, backend.Backend.DoShutdownNow()));
            })));
        }
        int ticks = 0;
        while (tasks.Any())
        {
            if (ticks++ > 20)
            {
                ticks = 0;
                Logs.Info($"Still waiting for {tasks.Count} backends to shut down ({string.Join(", ", tasks.Select(p => p.Item1).Select(b => $"{b.ID}: {b.Backend.HandlerTypeData.Name}"))})...");
            }
            Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();
            tasks = tasks.Where(t => !t.Item2.IsCompleted).ToList();
        }
        if (BackendsEdited)
        {
            Logs.Info("All backends shut down, saving file...");
            Save();
            Logs.Info("Backend handler shutdown complete.");
        }
        else
        {
            Logs.Info("Backend handler shutdown complete without saving.");
        }
    }

    /// <summary>Helper data for a model being requested, used to inform backend model switching choices.</summary>
    public class ModelRequestPressure
    {
        /// <summary>The requested modeld.</summary>
        public T2IModel Model;

        /// <summary>The time (TickCount64) of the first current request for this model.</summary>
        public long TimeFirstRequest = Environment.TickCount64;

        /// <summary>How many requests are waiting.</summary>
        public int Count;

        /// <summary>Whether something is currently loading for this request.</summary>
        public volatile bool IsLoading;

        /// <summary>Sessions that want the model.</summary>
        public HashSet<Session> Sessions = new();

        /// <summary>Set of backends that tried to satisfy this request but failed.</summary>
        public HashSet<int> BadBackends = new();

        /// <summary>Async issue prevention lock.</summary>
        public LockObject Locker = new();

        /// <summary>Gets a loose heuristic for model order preference - sort by earliest requester, but higher count of requests is worth 10 seconds.</summary>
        public long Heuristic(long timeRel) => Count * 10 + ((timeRel - TimeFirstRequest) / 1000); // TODO: 10 -> ?
    }

    /// <summary>Used by <see cref="GetNextT2IBackend(TimeSpan, T2IModel)"/> to determine which model to load onto a backend, heuristically.</summary>
    public ConcurrentDictionary<string, ModelRequestPressure> ModelRequests = new();

    /// <summary>Helper just for debug IDs for backend requests coming in.</summary>
    public static long BackendRequestsCounter = 0;

    /// <summary>Internal tracker of data related to a pending T2I Backend request.</summary>
    public class T2IBackendRequest
    {
        public BackendHandler Handler;

        public T2IModel Model;

        public Session Session;

        public long ID = Interlocked.Increment(ref BackendRequestsCounter);

        public Func<T2IBackendData, bool> Filter;

        public Action NotifyWillLoad;

        public long StartTime = Environment.TickCount64;

        public TimeSpan Waited => TimeSpan.FromMilliseconds(Environment.TickCount64 - StartTime);

        public ModelRequestPressure Pressure = null;

        public CancellationToken Cancel;

        public AsyncAutoResetEvent CompletedEvent = new(false);

        public T2IBackendAccess Result;

        public Exception Failure;

        public void ReleasePressure()
        {
            if (Pressure is null)
            {
                return;
            }
            Pressure.Count--;
            if (Pressure.Count == 0)
            {
                Handler.ModelRequests.TryRemove(Pressure.Model.Name, out _);
            }
            Pressure = null;
        }

        public void Complete()
        {
            Logs.Debug($"[BackendHandler] Backend request #{ID} finished.");
            Handler.T2IBackendRequests.TryRemove(ID, out _);
            ReleasePressure();
        }

        public void TryFind()
        {
            List<T2IBackendData> currentBackends = Handler.T2IBackends.Values.ToList();
            List<T2IBackendData> possible = currentBackends.Where(b => b.Backend.IsEnabled && !b.Backend.Reserved && b.Backend.Status == BackendStatus.RUNNING).ToList();
            Logs.Verbose($"[BackendHandler] Backend request #{ID} searching for backend... have {possible.Count}/{currentBackends.Count} possible");
            if (!possible.Any())
            {
                if (!currentBackends.Any(b => b.Backend.Status == BackendStatus.LOADING || b.Backend.Status == BackendStatus.WAITING))
                {
                    Logs.Warning("[BackendHandler] No backends are available! Cannot generate anything.");
                    Failure = new InvalidOperationException("No backends available!");
                }
                return;
            }
            possible = Filter is null ? possible : possible.Where(Filter).ToList();
            if (!possible.Any())
            {
                Logs.Warning("[BackendHandler] No backends match the request! Cannot generate anything.");
                Failure = new InvalidOperationException("No backends match the settings of the request given!");
                return;
            }
            List<T2IBackendData> available = possible.Where(b => !b.IsInUse).ToList();
            T2IBackendData firstAvail = available.FirstOrDefault();
            if (Model is null && firstAvail is not null)
            {
                Logs.Debug($"[BackendHandler] Backend request #{ID} will claim #{firstAvail.ID}");
                Result = new T2IBackendAccess(firstAvail);
                return;
            }
            if (Model is not null)
            {
                List<T2IBackendData> correctModel = available.Where(b => b.Backend.CurrentModelName == Model.Name).ToList();
                if (correctModel.Any())
                {
                    T2IBackendData backend = correctModel.FirstOrDefault();
                    Logs.Debug($"[BackendHandler] Backend request #{ID} found correct model on #{backend.ID}");
                    Result = new T2IBackendAccess(backend);
                    return;
                }
            }
            if (Pressure is null && Model is not null)
            {
                Logs.Verbose($"[BackendHandler] Backend request #{ID} is creating pressure for model {Model.Name}...");
                Pressure = Handler.ModelRequests.GetOrCreate(Model.Name, () => new() { Model = Model });
                lock (Pressure.Locker)
                {
                    Pressure.Count++;
                    if (Session is not null)
                    {
                        Pressure.Sessions.Add(Session);
                    }
                }
            }
            if (available.Any())
            {
                Handler.LoadHighestPressureNow(possible, available, ReleasePressure, Cancel);
            }
            if (Pressure is not null && Pressure.IsLoading && NotifyWillLoad is not null)
            {
                NotifyWillLoad();
                NotifyWillLoad = null;
            }
        }
    }

    /// <summary>All currently tracked T2I backend requests.</summary>
    public ConcurrentDictionary<long, T2IBackendRequest> T2IBackendRequests = new();

    /// <summary>Number of currently waiting backend requests.</summary>
    public int QueuedRequests => T2IBackendRequests.Count;

    /// <summary>(Blocking) gets the next available Text2Image backend.</summary>
    /// <returns>A 'using'-compatible wrapper for a backend.</returns>
    /// <param name="maxWait">Maximum duration to wait for. If time runs out, throws <see cref="TimeoutException"/>.</param>
    /// <param name="model">The model to use, or null for any. Specifying a model directly will prefer a backend with that model loaded, or cause a backend to load it if not available.</param>
    /// <param name="filter">Optional genericfilter for backend acceptance.</param>
    /// <param name="session">The session responsible for this request, if any.</param>
    /// <param name="notifyWillLoad">Optional callback for when this request will trigger a model load.</param>
    /// <param name="cancel">Optional request cancellation.</param>
    /// <exception cref="TimeoutException">Thrown if <paramref name="maxWait"/> is reached.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no backends are available.</exception>
    public async Task<T2IBackendAccess> GetNextT2IBackend(TimeSpan maxWait, T2IModel model = null, Func<T2IBackendData, bool> filter = null, Session session = null, Action notifyWillLoad = null, CancellationToken cancel = default)
    {
        if (HasShutdown)
        {
            throw new InvalidOperationException("Backend handler is shutting down.");
        }
        T2IBackendRequest request = new()
        {
            Handler = this,
            Model = model,
            Filter = filter,
            Session = session,
            NotifyWillLoad = notifyWillLoad,
            Cancel = cancel
        };
        T2IBackendRequests[request.ID] = request;
        CheckBackendsSignal.Set();
        try
        {
            Logs.Debug($"[BackendHandler] Backend request #{request.ID} for model {model?.Name ?? "any"}, maxWait={maxWait}.");
            if (!request.Cancel.CanBeCanceled)
            {
                request.Cancel = Program.GlobalProgramCancel;
            }
            await request.CompletedEvent.WaitAsync(maxWait, request.Cancel);
            if (request.Result is not null)
            {
                return request.Result;
            }
            else if (request.Failure is not null)
            {
                Logs.Error($"[BackendHandler] Backend request #{request.ID} failed: {request.Failure}");
                throw request.Failure;
            }
            if (request.Cancel.IsCancellationRequested || Program.GlobalProgramCancel.IsCancellationRequested)
            {
                return null;
            }
            string modelData = model is null ? "No model requested." : $"Requested model {model.Name}, which is loaded on {T2IBackends.Values.Count(b => b.Backend.CurrentModelName == model.Name)} backends.";
            Logs.Info($"[BackendHandler] Backend usage timeout, all backends occupied, giving up after {request.Waited.TotalSeconds} seconds ({modelData}).");
            throw new TimeoutException();
        }
        finally
        {
            request.Complete();
        }
    }

    /// <summary>Primary internal loop thread to handles tracking of backend requests.</summary>
    public void RequestHandlingLoop()
    {
        Logs.Init("Backend request handler loop ready...");
        long lastUpdate = Environment.TickCount64;
        while (true)
        {
            if (HasShutdown)
            {
                Logs.Info("Backend request handler loop closing...");
                foreach (T2IBackendRequest request in T2IBackendRequests.Values.ToArray())
                {
                    request.CompletedEvent.Set();
                }
                return;
            }
            try
            {
                foreach (T2IBackendRequest request in T2IBackendRequests.Values.ToArray())
                {
                    if (request.Cancel.IsCancellationRequested)
                    {
                        T2IBackendRequests.TryRemove(request.ID, out _);
                        request.CompletedEvent.Set();
                        continue;
                    }
                    request.TryFind();
                    if (request.Result is not null || request.Failure is not null)
                    {
                        T2IBackendRequests.TryRemove(request.ID, out _);
                        request.CompletedEvent.Set();
                        lastUpdate = Environment.TickCount64;
                    }
                }
                if (T2IBackendRequests.IsEmpty())
                {
                    lastUpdate = Environment.TickCount64;
                }
                else if (Environment.TickCount64 - lastUpdate > Program.ServerSettings.Backends.MaxTimeoutMinutes * 60 * 1000)
                {
                    lastUpdate = Environment.TickCount64;
                    Logs.Error($"[BackendHandler] {T2IBackendRequests.Count} requests denied due to backend timeout failure. Server backends are failing to respond.");
                    foreach (T2IBackendRequest request in T2IBackendRequests.Values.ToArray())
                    {
                        request.Failure = new TimeoutException($"No backend has responded in {Program.ServerSettings.Backends.MaxTimeoutMinutes} minutes.");
                        request.CompletedEvent.Set();
                    }
                }
                CheckBackendsSignal.WaitAsync(TimeSpan.FromSeconds(1), Program.GlobalProgramCancel).Wait();
            }
            catch (Exception ex)
            {
                Logs.Error($"Backend handler loop error: {ex}");
                Task.Delay(2000, Program.GlobalProgramCancel).Wait(); // Delay a bit to be safe in case of repeating errors.
            }
        }
    }

    /// <summary>Internal helper route for <see cref="GetNextT2IBackend"/> to trigger a backend model load.</summary>
    public void LoadHighestPressureNow(List<T2IBackendData> possible, List<T2IBackendData> available, Action ReleasePressure, CancellationToken cancel)
    {
        Logs.Verbose($"[BackendHandler] Will load highest pressure model...");
        long timeRel = Environment.TickCount64;
        ModelRequestPressure highestPressure = ModelRequests.Values.Where(p => !p.IsLoading).OrderByDescending(p => p.Heuristic(timeRel)).FirstOrDefault();
        if (highestPressure is not null)
        {
            lock (highestPressure.Locker)
            {
                if (highestPressure.IsLoading) // Another thread already got here, let it take control.
                {
                    Logs.Verbose($"[BackendHandler] Cancelling highest-pressure load, another thread is handling it.");
                    return;
                }
                long timeWait = timeRel - highestPressure.TimeFirstRequest;
                if (possible.Count == 1 || timeWait > 1500)
                {
                    List<T2IBackendData> valid = available.Where(b => !highestPressure.BadBackends.Contains(b.ID)).ToList();
                    if (valid.IsEmpty())
                    {
                        Logs.Warning("[BackendHandler] All backends failed to load the model! Cannot generate anything.");
                        ReleasePressure();
                        throw new InvalidOperationException("All available backends failed to load the model.");
                    }
                    valid = available.Where(b => b.Backend.CurrentModelName != highestPressure.Model.Name).ToList();
                    if (valid.IsEmpty())
                    {
                        Logs.Verbose("$[BackendHandler] Cancelling highest-pressure load, model is already loaded on all available backends.");
                        return;
                    }
                    T2IBackendData availableBackend = valid.MinBy(a => a.TimeLastRelease);
                    Logs.Debug($"[BackendHandler] backend #{availableBackend.ID} will load a model: {highestPressure.Model.Name}, with {highestPressure.Count} requests waiting for {timeWait / 1000f:0.#} seconds");
                    highestPressure.IsLoading = true;
                    List<Session.GenClaim> claims = new();
                    foreach (Session sess in highestPressure.Sessions)
                    {
                        claims.Add(sess.Claim(0, 1, 0, 0));
                    }
                    T2IBackendAccess access = new(availableBackend);
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            availableBackend.Backend.LoadModel(highestPressure.Model).Wait(cancel);
                            Logs.Debug($"[BackendHandler] backend #{availableBackend.ID} loaded model, returning to pool");
                        }
                        catch (Exception ex)
                        {
                            Logs.Error($"[BackendHandler] backend #{availableBackend.ID} failed to load model with error: {ex}");
                        }
                        finally
                        {
                            if (availableBackend.Backend.CurrentModelName != highestPressure.Model.Name)
                            {
                                Logs.Warning($"[BackendHandler] backend #{availableBackend.ID} failed to load model {highestPressure.Model.Name}");
                                lock (highestPressure.Locker)
                                {
                                    highestPressure.BadBackends.Add(availableBackend.ID);
                                }
                            }
                            highestPressure.IsLoading = false;
                            foreach (Session.GenClaim claim in claims)
                            {
                                claim.Dispose();
                            }
                            access.Dispose();
                        }
                    }, cancel);
                }
                else
                {
                    Logs.Verbose($"[BackendHandler] Nothing to load onto right now, pressure is too new.");
                }
            }
        }
    }
}

/// <summary>Mini-helper to track a backend accessor's status and release the access claim when done.</summary>
public class T2IBackendAccess : IDisposable
{
    /// <summary>The data for the backend that's claimed.</summary>
    public BackendHandler.T2IBackendData Data;

    public T2IBackendAccess(BackendHandler.T2IBackendData _data)
    {
        Data = _data;
        Data.Claim();
    }

    /// <summary>The backend that's claimed.</summary>
    public AbstractT2IBackend Backend => Data.Backend;

    private bool IsDisposed = false;

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            Data.TimeLastRelease = Environment.TickCount64;
            Data.IsInUse = false;
            Backend.Handler.CheckBackendsSignal.Set();
            GC.SuppressFinalize(this);
        }
    }
}
