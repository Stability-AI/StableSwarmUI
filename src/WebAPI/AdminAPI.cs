using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Hardware.Info;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace StableSwarmUI.WebAPI;

[API.APIClass("Administrative APIs related to server management.")]
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
        API.RegisterAPICall(DebugGenDocs, true);
        API.RegisterAPICall(ListConnectedUsers);
        API.RegisterAPICall(UpdateAndRestart);
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
    [API.APIDescription("Returns a list of the server settings, will full metadata.",
        """
            "settings": {
                "settingname": {
                    "type": "typehere",
                    "name": "namehere",
                    "value": somevaluehere,
                    "description": "sometext",
                    "values": [...] or null,
                    "value_names": [...] or null
                }
            }
        """)]
    public static async Task<JObject> ListServerSettings(Session session)
    {
        return new JObject() { ["settings"] = AutoConfigToParamData(Program.ServerSettings) };
    }

    [API.APIDescription("Changes server settings.", "\"success\": true")]
    public static async Task<JObject> ChangeServerSettings(Session session,
        [API.APIParameter("Dynamic input of `\"settingname\": valuehere`.")] JObject rawData)
    {
        Logs.Warning($"User {session.User.UserID} changed server settings.");
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

    [API.APIDescription("Returns a list of the available log types.",
        """
            "types_available": [
                {
                    "name": "namehere",
                    "color": "#RRGGBB",
                    "identifier": "identifierhere"
                }
            ]
        """)]
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

    [API.APIDescription("Returns a list of recent server log messages.",
        """
          "last_sequence_id": 123,
          "data": {
                "info": [
                    {
                        "sequence_id": 123,
                        "timestamp": "yyyy-MM-dd HH:mm:ss.fff",
                        "message": "messagehere"
                    }, ...
                ]
            }
        """)]
    public static async Task<JObject> ListRecentLogMessages(Session session,
        [API.APIParameter("Optionally input `\"last_sequence_ids\": { \"info\": 123 }` to set the start point.")] JObject raw)
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

    [API.APIDescription("Shuts the server down. Returns success before the server is gone.", "\"success\": true")]
    public static async Task<JObject> ShutdownServer(Session session)
    {
        Logs.Warning($"User {session.User.UserID} requested server shutdown.");
        _ = Task.Run(() => Program.Shutdown());
        return new JObject() { ["success"] = true };
    }

    [API.APIDescription("Returns information about the server's resource usage.",
        """
            "cpu": {
                "usage": 0.0,
                "cores": 0
            },
            "system_ram": {
                "total": 0,
                "used": 0,
                "free": 0
            },
            "gpus": {
                "0": {
                    "id": 0,
                    "name": "namehere",
                    "temperature": 0,
                    "utilization_gpu": 0,
                    "utilization_memory": 0,
                    "total_memory": 0,
                    "free_memory": 0,
                    "used_memory": 0
                }
            }
        """
               )]
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

    [API.APIDescription("(Internal/Debug route), adds language data to the language file builder.", "\"success\": true")]
    public static async Task<JObject> DebugLanguageAdd(Session session,
        [API.APIParameter("\"set\": [ \"word\", ... ]")] JObject raw)
    {
        LanguagesHelper.TrackSet(raw["set"].ToArray().Select(v => $"{v}").ToArray());
        return new JObject() { ["success"] = true };
    }

    [API.APIDescription("(Internal/Debug route), generates API docs.", "\"success\": true")]
    public static async Task<JObject> DebugGenDocs(Session session)
    {
        await API.GenerateAPIDocs();
        return new JObject() { ["success"] = true };
    }

    [API.APIDescription("Returns a list of currently connected users.",
        """
            "users":
            [
                {
                    "id": "useridhere",
                    "last_active_seconds": 0,
                    "active_sessions": [ "addresshere", "..." ],
                    "last_active": "10 seconds ago"
                }
            ]
        """)]
    public static async Task<JObject> ListConnectedUsers(Session session)
    {
        static JArray sessWrangle(IEnumerable<string> addresses)
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

    [API.APIDescription("Causes swarm to update, then close and restart itself. If there's no update to apply, won't restart.",
        """
            "success": true, // or false if not updated
            "result": "No changes found." // or any other applicable human-readable English message
        """)]
    public static async Task<JObject> UpdateAndRestart(Session session)
    {
        Logs.Warning($"User {session.User.UserID} requested update-and-restart.");
        static async Task<string> launchGit(string args)
        {
            ProcessStartInfo start = new("git", args)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            Process p = Process.Start(start);
            await p.WaitForExitAsync(Program.GlobalProgramCancel);
            return await p.StandardOutput.ReadToEndAsync();
        }
        string priorHash = (await launchGit("rev-parse HEAD")).Trim();
        await launchGit("pull");
        string localHash = (await launchGit("rev-parse HEAD")).Trim();
        Logs.Debug($"Update checker: prior hash was {priorHash}, new hash is {localHash}");
        if (priorHash == localHash)
        {
            return new JObject() { ["success"] = false, ["result"] = "No changes found." };
        }
        File.WriteAllText("src/bin/must_rebuild", "yes");
        _ = Utilities.RunCheckedTask(() => Program.Shutdown(42));
        return new JObject() { ["success"] = true, ["result"] = "Update successful. Restarting... (please wait a moment, then refresh the page)" };
    }
}
