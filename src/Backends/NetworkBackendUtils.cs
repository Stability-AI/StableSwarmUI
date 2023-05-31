using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableUI.Utils;

namespace StableUI.Backends;

public static class NetworkBackendUtils
{

    public static bool IsValidStartPath(string backendLabel, string path, string ext)
    {
        if (path.Length < 5)
        {
            return false;
        }
        if (ext != "sh" && ext != "bat")
        {
            Logs.Error($"Refusing init of {backendLabel} with non-script target. Please verify your start script location.");
            return false;
        }
        if (path.AfterLast('/').BeforeLast('.') == "webui-user")
        {
            Logs.Error($"Refusing init of {backendLabel} with 'web-ui' target script. Please use the 'webui' script instead.");
            return false;
        }
        string subPath = path[1] == ':' ? path[2..] : path;
        if (Utilities.FilePathForbidden.ContainsAnyMatch(subPath))
        {
            Logs.Error($"Failed init of {backendLabel} with script target '{path}' because that file path contains invalid characters ( {Utilities.FilePathForbidden.TrimToMatches(subPath)} ). Please verify your start script location.");
            return false;
        }
        if (!File.Exists(path))
        {
            Logs.Error($"Failed init of {backendLabel} with script target '{path}' because that file does not exist. Please verify your start script location.");
            return false;
        }
        return true;
    }

    public static async Task<JType> Parse<JType>(HttpResponseMessage message) where JType : class
    {
        string content = await message.Content.ReadAsStringAsync();
        if (typeof(JType) == typeof(JObject)) // TODO: Surely C# has syntax for this?
        {
            return JObject.Parse(content) as JType;
        }
        else if (typeof(JType) == typeof(JArray))
        {
            return JArray.Parse(content) as JType;
        }
        else if (typeof(JType) == typeof(string))
        {
            return content as JType;
        }
        throw new NotImplementedException();
    }
}
