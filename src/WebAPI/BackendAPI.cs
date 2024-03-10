using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;

namespace StableSwarmUI.WebAPI;

public class BackendAPI
{
    public static void Register()
    {
        API.RegisterAPICall(ListBackendTypes);
        API.RegisterAPICall(ListBackends);
        API.RegisterAPICall(DeleteBackend);
        API.RegisterAPICall(ToggleBackend);
        API.RegisterAPICall(EditBackend);
        API.RegisterAPICall(AddNewBackend);
        API.RegisterAPICall(RestartBackends);
        API.RegisterAPICall(FreeBackendMemory);
    }

    /// <summary>API route to list currently available backend-types.</summary>
    public static async Task<JObject> ListBackendTypes()
    {
        return new() { ["list"] = JToken.FromObject(Program.Backends.BackendTypes.Values.Select(b => b.NetDescription).ToList()) };
    }

    /// <summary>Create a network object to represent a backend cleanly.</summary>
    public static JObject BackendToNet(BackendHandler.T2IBackendData backend, bool full = false)
    {
        JObject data = new()
        {
            ["type"] = backend.Backend.HandlerTypeData.ID,
            ["status"] = backend.Backend.Status.ToString().ToLowerFast(),
            ["id"] = backend.ID,
            ["settings"] = JToken.FromObject(backend.Backend.SettingsRaw.Save(true).ToSimple()),
            ["modcount"] = backend.ModCount,
            ["features"] = new JArray(backend.Backend.SupportedFeatures.ToArray()),
            ["enabled"] = backend.Backend.IsEnabled,
            ["title"] = backend.Backend.Title,
            ["can_load_models"] = backend.Backend.CanLoadModels,
            ["max_usages"] = backend.Backend.MaxUsages
        };
        if (full)
        {
            data["current_model"] = backend.Backend.CurrentModelName;
        }
        return data;
    }

    /// <summary>API route to shutdown and delete a registered backend.</summary>
    public static async Task<JObject> DeleteBackend(int backend_id)
    {
        if (Program.LockSettings)
        {
            return new() { ["error"] = "Settings are locked." };
        }
        if (await Program.Backends.DeleteById(backend_id))
        {
            return new JObject() { ["result"] = "Deleted." };
        }
        return new JObject() { ["result"] = "Already didn't exist." };
    }

    /// <summary>API route to disable or re-enable a backend.</summary>
    public static async Task<JObject> ToggleBackend(int backend_id, bool enabled)
    {
        if (Program.LockSettings)
        {
            return new() { ["error"] = "Settings are locked." };
        }
        if (!Program.Backends.T2IBackends.TryGetValue(backend_id, out BackendHandler.T2IBackendData backend))
        {
            return new() { ["error"] = $"Invalid backend ID {backend_id}" };
        }
        if (backend.Backend.IsEnabled == enabled)
        {
            return new JObject() { ["result"] = "No change." };
        }
        backend.Backend.IsEnabled = enabled;
        backend.Backend.ShutDownReserve = true;
        Program.Backends.BackendsEdited = true;
        while (backend.CheckIsInUse)
        {
            if (Program.GlobalProgramCancel.IsCancellationRequested)
            {
                return null;
            }
            await Task.Delay(TimeSpan.FromSeconds(0.5));
        }
        if (backend.Backend.Status != BackendStatus.DISABLED && backend.Backend.Status != BackendStatus.ERRORED)
        {
            await backend.Backend.DoShutdownNow();
        }
        if (enabled)
        {
            backend.Backend.Status = BackendStatus.WAITING;
            Program.Backends.BackendsToInit.Enqueue(backend);
        }
        backend.Backend.ShutDownReserve = false;
        return new JObject() { ["result"] = "Success." };
    }

    /// <summary>API route to modify and re-init an already registered backend.</summary>
    public static async Task<JObject> EditBackend(int backend_id, string title, JObject raw_inp)
    {
        if (Program.LockSettings)
        {
            return new() { ["error"] = "Settings are locked." };
        }
        if (!raw_inp.TryGetValue("settings", out JToken jval) || jval is not JObject settings)
        {
            return new() { ["error"] = "Missing settings." };
        }
        FDSSection parsed = FDSSection.FromSimple(settings.ToBasicObject());
        BackendHandler.T2IBackendData result = await Program.Backends.EditById(backend_id, parsed, title);
        if (result is null)
        {
            return new() { ["error"] = $"Invalid backend ID {backend_id}" };
        }
        return BackendToNet(result);
    }

    /// <summary>API route to list currently registered backends.</summary>
    public static async Task<JObject> ListBackends(bool nonreal = false, bool full_data = false)
    {
        JObject toRet = [];
        foreach (BackendHandler.T2IBackendData data in Program.Backends.T2IBackends.Values.OrderBy(d => d.ID))
        {
            if (!data.Backend.IsReal && !nonreal)
            {
                continue;
            }
            toRet[data.ID.ToString()] = BackendToNet(data, full_data);
        }
        return toRet;
    }

    /// <summary>API route to add a new backend.</summary>
    public static async Task<JObject> AddNewBackend(string type_id)
    {
        if (Program.LockSettings)
        {
            return new() { ["error"] = "Settings are locked." };
        }
        if (!Program.Backends.BackendTypes.TryGetValue(type_id, out BackendHandler.BackendType type))
        {
            return new() { ["error"] = $"Invalid backend type: {type_id}" };
        }
        BackendHandler.T2IBackendData data = Program.Backends.AddNewOfType(type);
        return BackendToNet(data);
    }

    /// <summary>API route to restart all backends.</summary>
    public static async Task<JObject> RestartBackends(string backend = "all")
    {
        if (Program.LockSettings)
        {
            return new() { ["error"] = "Settings are locked." };
        }
        int count = 0;
        foreach (BackendHandler.T2IBackendData data in Program.Backends.T2IBackends.Values)
        {
            if (backend != "all" && backend != $"{data.ID}")
            {
                continue;
            }
            if (data.Backend.Status == BackendStatus.RUNNING || data.Backend.Status == BackendStatus.ERRORED)
            {
                await Program.Backends.ShutdownBackendCleanly(data);
                Program.Backends.DoInitBackend(data);
                count++;
            }
        }
        return new JObject() { ["result"] = "Success.", ["count"] = count };
    }

    /// <summary>API route to free memory from all backends.</summary>
    public static async Task<JObject> FreeBackendMemory(bool system_ram = false, string backend = "all")
    {
        List<Task> tasks = [];
        foreach (AbstractT2IBackend target in Program.Backends.RunningBackendsOfType<AbstractT2IBackend>())
        {
            if (backend != "all" && backend != $"{target.BackendData.ID}")
            {
                continue;
            }
            tasks.Add(target.FreeMemory(system_ram));
        }
        if (tasks.IsEmpty())
        {
            return new JObject() { ["result"] = false, ["count"] = 0 };
        }
        await Task.WhenAll(tasks);
        Utilities.CleanRAM();
        return new JObject() { ["result"] = true, ["count"] = tasks.Count };
    }
}
