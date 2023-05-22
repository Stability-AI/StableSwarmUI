using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Text2Image;
using StableUI.Utils;
using System.Collections.Concurrent;
using System.Reflection;

namespace StableUI.Backends;

/// <summary>Central manager for available backends.</summary>
public class BackendHandler
{
    /// <summary>Currently loaded backends. Might not all be valid.</summary>
    public ConcurrentDictionary<int, T2IBackendData> T2IBackends = new();

    /// <summary>Signal when any backends are available.</summary>
    public AutoResetEvent BackendsAvailableSignal = new(false);

    /// <summary>Central locker to prevent issues with backend validating.</summary>
    public LockObject CentralLock = new();

    /// <summary>Map of backend type IDs to metadata about them.</summary>
    public Dictionary<string, BackendType> BackendTypes = new();

    /// <summary>Value to ensure unique IDs are given to new backends.</summary>
    public int LastBackendID = 0;

    /// <summary>The path to where the backend list is saved.</summary>
    public string SaveFilePath = "Data/Backends.fds";

    /// <summary>Queue of backends to initialize.</summary>
    public ConcurrentQueue<T2IBackendData> BackendsToInit = new();

    /// <summary>Signal for when a new backend is added to <see cref="BackendsToInit"/>.</summary>
    public AutoResetEvent NewBackendInitSignal = new(false);

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
    public void RegisterBackendType(Type type, string id, string name, string description)
    {
        Type settingsType = type.GetField(nameof(AbstractT2IBackend<AutoConfiguration>.Settings)).FieldType;
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
        BackendTypes.Add(id, new BackendType(id, name, description, settingsType, settingsInternal, type, netDesc));
    }

    /// <summary>Register a new backend-type by type ref.</summary>
    public void RegisterBackendType<T>(string id, string name, string description) where T : AbstractT2IBackend
    {
        RegisterBackendType(typeof(T), id, name, description);
    }

    /// <summary>Special live data about a registered backend.</summary>
    public class T2IBackendData
    {
        public AbstractT2IBackend Backend;

        public volatile bool IsInUse = false;

        public LockObject AccessLock = new();

        public int ID;

        public int InitAttempts = 0;

        public int ModCount = 0;
    }

    /// <summary>Adds a new backend of the given type, and returns its data. Note that the backend will not be initialized at first.</summary>
    public T2IBackendData AddNewOfType(BackendType type)
    {
        T2IBackendData data = new()
        {
            Backend = Activator.CreateInstance(type.BackendClass) as AbstractT2IBackend
        };
        data.Backend.InternalSettingsAccess = Activator.CreateInstance(type.SettingsClass) as AutoConfiguration;
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

    /// <summary>Shutdown and delete a given backend.</summary>
    public async Task<bool> DeleteById(int id)
    {
        if (!T2IBackends.TryRemove(id, out T2IBackendData data))
        {
            return false;
        }
        while (data.IsInUse)
        {
            await Task.Delay(TimeSpan.FromSeconds(0.5));
        }
        await data.Backend.Shutdown();
        ReassignLoadedModelsList();
        return true;
    }

    /// <summary>Replace the settings of a given backend. Shuts it down immediately and queues a reload.</summary>
    public async Task<T2IBackendData> EditById(int id, FDSSection newSettings)
    {
        if (!T2IBackends.TryGetValue(id, out T2IBackendData data))
        {
            return null;
        }
        while (data.IsInUse)
        {
            await Task.Delay(TimeSpan.FromSeconds(0.5));
        }
        await data.Backend.Shutdown();
        data.Backend.InternalSettingsAccess.Load(newSettings);
        data.ModCount++;
        data.Backend.Status = BackendStatus.WAITING;
        BackendsToInit.Enqueue(data);
        return data;
    }

    /// <summary>Loads the backends list from a file.</summary>
    public void Load()
    {
        if (T2IBackends.Any()) // Backup to prevent duplicate calls
        {
            return;
        }
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
            data.Backend.InternalSettingsAccess = Activator.CreateInstance(type.SettingsClass) as AutoConfiguration;
            data.Backend.InternalSettingsAccess.Load(section.GetSection("settings"));
            data.Backend.HandlerTypeData = type;
            data.Backend.Handler = this;
            data.Backend.Status = BackendStatus.WAITING;
            BackendsToInit.Enqueue(data);
            lock (CentralLock)
            {
                T2IBackends.TryAdd(data.ID, data);
            }
        }
        NewBackendInitSignal.Set();
        ReassignLoadedModelsList();
    }

    /// <summary>Internal thread path for processing new backend initializations.</summary>
    public void InternalInitMonitor()
    {
        while (!HasShutdown)
        {
            bool any = false;
            while (BackendsToInit.TryDequeue(out T2IBackendData data))
            {
                try
                {
                    data.InitAttempts++;
                    data.Backend.Init().Wait();
                    any = true;
                }
                catch (Exception ex)
                {
                    if (data.InitAttempts <= Program.ServerSettings.MaxBackendInitAttempts)
                    {
                        data.Backend.Status = BackendStatus.WAITING;
                        Logs.Error($"Error #{data.InitAttempts} while initializing backend {data.Backend.HandlerTypeData.Name} #{data.ID} - will retry");
                        BackendsToInit.Enqueue(data);
                    }
                    else
                    {
                        data.Backend.Status = BackendStatus.ERRORED;
                        Logs.Error($"Final error ({data.InitAttempts}) while initializing backend {data.Backend.HandlerTypeData.Name} #{data.ID}, giving up: {ex}");
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
            NewBackendInitSignal.WaitOne(TimeSpan.FromSeconds(2));
        }
    }

    /// <summary>Updates what model(s) are currently loaded.</summary>
    public void ReassignLoadedModelsList()
    {
        foreach (T2IModel model in Program.T2IModels.Models.Values)
        {
            model.AnyBackendsHaveLoaded = false;
        }
        foreach (T2IBackendData backend in T2IBackends.Values)
        {
            if (backend.Backend.CurrentModelName is not null && Program.T2IModels.Models.TryGetValue(backend.Backend.CurrentModelName, out T2IModel model))
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
            FDSSection data_section = new();
            data_section.Set("type", data.Backend.HandlerTypeData.ID);
            data_section.Set("settings", data.Backend.InternalSettingsAccess.Save(true));
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
                    while (backend.IsInUse)
                    {
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
        BackendsAvailableSignal.Set();
        List<Task> tasks = new();
        foreach (T2IBackendData backend in T2IBackends.Values)
        {
            while (backend.IsInUse)
            {
                Thread.Sleep(100);
            }
            tasks.Add(backend.Backend.Shutdown());
        }
        Task.WaitAll(tasks.ToArray());
        Save();
    }

    /// <summary>(Blocking) gets the next available Text2Image backend.</summary>
    /// <returns>A 'using'-compatible wrapper for a backend.</returns>
    /// <param name="maxWait">Maximum duration to wait for. If time runs out, throws <see cref="TimeoutException"/>.</param>
    /// <exception cref="TimeoutException">Thrown if <paramref name="maxWait"/> is reached.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no backends are available.</exception>
    public T2IBackendAccess GetNextT2IBackend(TimeSpan maxWait)
    {
        long startTime = Environment.TickCount64;
        while (true)
        {
            if (HasShutdown)
            {
                throw new InvalidOperationException("Backend handler is shutting down.");
            }
            TimeSpan waited = new(Environment.TickCount64 - startTime);
            if (waited > maxWait)
            {
                Logs.Info($"Backend usage timeout, all backends occupied, giving up after {waited.TotalSeconds} seconds.");
                throw new TimeoutException();
            }
            if (!T2IBackends.Values.Any(b => b.Backend.Status == BackendStatus.RUNNING))
            {
                Logs.Warning("No backends are available! Cannot generate anything.");
                throw new InvalidOperationException("No backends available!");
            }
            lock (CentralLock)
            {
                foreach (T2IBackendData backend in T2IBackends.Values)
                {
                    if (!backend.IsInUse && backend.Backend.Status == BackendStatus.RUNNING)
                    {
                        backend.IsInUse = true;
                        return new T2IBackendAccess(backend);
                    }
                }
            }
            BackendsAvailableSignal.WaitOne(TimeSpan.FromSeconds(2));
        }
    }
}

public record class T2IBackendAccess(BackendHandler.T2IBackendData Data) : IDisposable
{
    public AbstractT2IBackend Backend => Data.Backend;

    private bool IsDisposed = false;

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            Data.IsInUse = false;
            Backend.Handler.BackendsAvailableSignal.Set();
            GC.SuppressFinalize(this);
        }
    }
}
