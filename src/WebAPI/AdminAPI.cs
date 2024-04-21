using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Hardware.Info;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.Reflection;

namespace StableSwarmUI.WebAPI;

public static class AdminAPI
{
    public static void Register()
    {
        API.RegisterAPICall(ListServerSettings);
        API.RegisterAPICall(ChangeServerSettings, true);
        API.RegisterAPICall(ListLogTypes);
        API.RegisterAPICall(ListRecentLogMessages);
        API.RegisterAPICall(ShutdownServer, true);
        API.RegisterAPICall(GetServerResourceInfo);
        API.RegisterAPICall(DebugLanguageAdd, true);
        API.RegisterAPICall(ListConnectedUsers);
    }

    public static JObject AutoConfigToParamData(AutoConfiguration config)
    {
        JObject output = [];
        foreach ((string key, AutoConfiguration.Internal.SingleFieldData data) in config.InternalData.SharedData.Fields)
        {
            string typeName = data.IsSection ? "group" : T2IParamTypes.SharpTypeToDataType(data.Field.FieldType, false).ToString();
            if (typeName is null || typeName == T2IParamDataType.UNSET.ToString())
            {
                throw new Exception($"[ServerSettings] Unknown type '{data.Field.FieldType}' for field '{data.Field.Name}'!");
            }
            object val = config.GetFieldValueOrDefault<object>(key);
            if (val is AutoConfiguration subConf)
            {
                val = AutoConfigToParamData(subConf);
            }
            string[] vals = data.Field.GetCustomAttribute<SettingsOptionsAttribute>()?.Options ?? null;
            string[] val_names = null;
            if (vals is not null)
            {
                typeName = "dropdown";
                val_names = data.Field.GetCustomAttribute<SettingsOptionsAttribute>()?.Names ?? null;
            }
            output[key] = new JObject()
            {
                ["type"] = typeName.ToLowerFast(),
                ["name"] = data.Name,
                ["value"] = JToken.FromObject(val is List<string> list ? list.JoinString(" || ") : val),
                ["description"] = data.Field.GetCustomAttribute<AutoConfiguration.ConfigComment>()?.Comments ?? "",
                ["values"] = vals == null ? null : new JArray(vals),
                ["value_names"] = val_names == null ? null : new JArray(val_names)
            };
        }
        return output;
    }

    public static object DataToType(JToken val, Type t)
    {
        if (t == typeof(int)) { return (int)val; }
        if (t == typeof(long)) { return (long)val; }
        if (t == typeof(double)) { return (double)val; }
        if (t == typeof(float)) { return (float)val; }
        if (t == typeof(bool)) { return (bool)val; }
        if (t == typeof(string)) { return (string)val; }
        if (t == typeof(List<string>)) { return ((JArray)val).Select(v => (string)v).ToList(); }
        return null;
    }

    /// <summary>API Route to list the server settings metadata.</summary>
    public static async Task<JObject> ListServerSettings(Session session)
    {
        return new JObject() { ["settings"] = AutoConfigToParamData(Program.ServerSettings) };
    }

    /// <summary>API Route to change server settings.</summary>
    public static async Task<JObject> ChangeServerSettings(Session session, JObject rawData)
    {
        JObject settings = (JObject)rawData["settings"];
        foreach ((string key, JToken val) in settings)
        {
            AutoConfiguration.Internal.SingleFieldData field = Program.ServerSettings.TryGetFieldInternalData(key, out _);
            if (field is null)
            {
                Logs.Error($"User '{session.User.UserID}' tried to set unknown server setting '{key}' to '{val}'.");
                continue;
            }
            object obj = DataToType(val, field.Field.FieldType);
            if (obj is null)
            {
                Logs.Error($"User '{session.User.UserID}' tried to set server setting '{key}' of type '{field.Field.FieldType.Name}' to '{val}', but type-conversion failed.");
                continue;
            }
            Program.ServerSettings.TrySetFieldValue(key, obj);
        }
        Program.SaveSettingsFile();
        if (settings.Properties().Any(p => p.Name.StartsWith("paths.")))
        {
            Program.BuildModelLists();
            Program.RefreshAllModelSets();
            Program.ModelPathsChangedEvent?.Invoke();
        }
        Program.ReapplySettings();
        return new JObject() { ["success"] = true };
    }

    /// <summary>API Route to list the current available log types.</summary>
    public static async Task<JObject> ListLogTypes(Session session)
    {
        JArray types = [];
        lock (Logs.OtherTrackers)
        {
            foreach ((string name, Logs.LogTracker tracker) in Logs.OtherTrackers)
            {
                types.Add(new JObject()
                {
                    ["name"] = name,
                    ["color"] = tracker.Color,
                    ["identifier"] = tracker.Identifier
                });
            }
        }
        return new JObject() { ["types_available"] = types };
    }

    /// <summary>API Route to get recent server logs.</summary>
    public static async Task<JObject> ListRecentLogMessages(Session session, JObject raw)
    {
        JObject result = await ListLogTypes(session);
        long lastSeq = Interlocked.Read(ref Logs.LogTracker.LastSequenceID);
        result["last_sequence_id"] = lastSeq;
        JObject messageData = [];
        List<string> types = raw["types"].Select(v => $"{v}").ToList();
        foreach (string type in types)
        {
            Logs.LogTracker tracker;
            lock (Logs.OtherTrackers)
            {
                if (!Logs.OtherTrackers.TryGetValue(type, out tracker))
                {
                    continue;
                }
            }
            JArray messages = [];
            messageData[type] = messages;
            long lastSeqId = -1;
            if ((raw["last_sequence_ids"] as JObject).TryGetValue(type, out JToken lastSeqIdToken))
            {
                lastSeqId = lastSeqIdToken.Value<long>();
            }
            if (tracker.LastSeq < lastSeqId)
            {
                continue;
            }
            lock (tracker.Lock)
            {
                foreach (Logs.LogMessage message in tracker.Messages)
                {
                    if (message.Sequence <= lastSeqId)
                    {
                        continue;
                    }
                    messages.Add(new JObject()
                    {
                        ["sequence_id"] = message.Sequence,
                        ["time"] = $"{message.Time:yyyy-MM-dd HH:mm:ss.fff}",
                        ["message"] = message.Message
                    });
                }
            }
        }
        result["data"] = messageData;
        return result;
    }

    /// <summary>API Route to shut the server down.</summary>
    public static async Task<JObject> ShutdownServer(Session session)
    {
        _ = Task.Run(Program.Shutdown);
        return new JObject() { ["success"] = true };
    }

    /// <summary>API Route to get information about server resource usage.</summary>
    public static async Task<JObject> GetServerResourceInfo(Session session)
    {
        NvidiaUtil.NvidiaInfo[] gpuInfo = NvidiaUtil.QueryNvidia();
        MemoryStatus memStatus = SystemStatusMonitor.HardwareInfo.MemoryStatus;
        JObject result = new()
        {
            ["cpu"] = new JObject()
            {
                ["usage"] = SystemStatusMonitor.ProcessCPUUsage,
                ["cores"] = Environment.ProcessorCount,
            },
            ["system_ram"] = new JObject()
            {
                ["total"] = memStatus.TotalPhysical,
                ["used"] = memStatus.TotalPhysical - memStatus.AvailablePhysical,
                ["free"] = memStatus.AvailablePhysical
            }
        };
        if (gpuInfo is not null)
        {
            JObject gpus = [];
            foreach (NvidiaUtil.NvidiaInfo gpu in gpuInfo)
            {
                gpus[$"{gpu.ID}"] = new JObject()
                {
                    ["id"] = gpu.ID,
                    ["name"] = gpu.GPUName,
                    ["temperature"] = gpu.Temperature,
                    ["utilization_gpu"] = gpu.UtilizationGPU,
                    ["utilization_memory"] = gpu.UtilizationMemory,
                    ["total_memory"] = gpu.TotalMemory.InBytes,
                    ["free_memory"] = gpu.FreeMemory.InBytes,
                    ["used_memory"] = gpu.UsedMemory.InBytes
                };
            }
            result["gpus"] = gpus;
        }
        return result;
    }

    /// <summary>API Route to shut the server down.</summary>
    public static async Task<JObject> DebugLanguageAdd(Session session, JObject raw)
    {
        LanguagesHelper.TrackSet(raw["set"].ToArray().Select(v => $"{v}").ToArray());
        return new JObject() { ["success"] = true };
    }

    /// <summary>API Route to get a list of currently connected users.</summary>
    public static async Task<JObject> ListConnectedUsers(Session session)
    {
        JArray sessWrangle(IEnumerable<string> addresses)
        {
            Dictionary<string, int> counts = [];
            foreach (string addr in addresses)
            {
                counts[addr] = counts.GetValueOrDefault(addr, 0) + 1;
            }
            JArray result = [];
            foreach ((string addr, int count) in counts)
            {
                result.Add(new JObject() { ["address"] = addr, ["count"] = count });
            }
            return result;
        }
        JArray list = new(Program.Sessions.Users.Values.Where(u => u.TimeSinceLastPresent.TotalMinutes < 3 && !u.UserID.StartsWith("__")).OrderBy(u => u.UserID).Select(u => new JObject()
        {
            ["id"] = u.UserID,
            ["last_active_seconds"] = u.TimeSinceLastUsed.TotalSeconds,
            ["active_sessions"] = sessWrangle(u.CurrentSessions.Values.Where(s => s.TimeSinceLastUsed.TotalMinutes < 3).Select(s => s.OriginAddress)),
            ["last_active"] = $"{u.TimeSinceLastUsed.SimpleFormat(false, false)} ago"
        }).ToArray());
        return new JObject() { ["users"] = list };
    }
}
