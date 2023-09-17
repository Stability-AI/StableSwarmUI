using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
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
        API.RegisterAPICall(ChangeServerSettings);
    }

    public static JObject AutoConfigToParamData(AutoConfiguration config)
    {
        JObject output = new();
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
            if (vals is not null)
            {
                typeName = "dropdown";
            }
            output[key] = new JObject()
            {
                ["type"] = typeName.ToLowerFast(),
                ["name"] = data.Name,
                ["value"] = JToken.FromObject(val is List<string> list ? list.JoinString(" || ") : val),
                ["description"] = data.Field.GetCustomAttribute<AutoConfiguration.ConfigComment>()?.Comments ?? "",
                ["values"] = vals == null ? null : new JArray(vals)
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
        Program.ReapplySettings();
        return new JObject() { ["success"] = true };
    }
}
