using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableUI.Core;
using StableUI.Utils;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace StableUI.Backends;

public static class NetworkBackendUtils
{
    public static HttpClient MakeHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"StableUI/{Utilities.Version}");
        client.Timeout = TimeSpan.FromMinutes(10);
        return client;
    }

    public static bool IsValidStartPath(string backendLabel, string path, string ext)
    {
        if (path.Length < 5)
        {
            return false;
        }
        if (ext != "sh" && ext != "bat" && ext != "py")
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
        try
        {
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
        }
        catch (JsonReaderException ex)
        {
            throw new InvalidOperationException($"Failed to read JSON '{content}' with message: {ex.Message}");
        }
        throw new NotImplementedException();
    }

    public static int NextPort = 7820;

    public static Task DoSelfStart(string startScript, AbstractT2IBackend backend, string nameSimple, int gpuId, string extraArgs, Func<bool, Task> initInternal, Action<int, Process> takeOutput)
    {
        return DoSelfStart(startScript, nameSimple, gpuId, extraArgs, status => backend.Status = status, async (b) => { await initInternal(b); return backend.Status == BackendStatus.RUNNING; }, takeOutput, () => backend.Status);
    }

    public static async Task DoSelfStart(string startScript, string nameSimple, int gpuId, string extraArgs, Action<BackendStatus> reviseStatus, Func<bool, Task<bool>> initInternal, Action<int, Process> takeOutput, Func<BackendStatus> getStatus)
    {
        if (string.IsNullOrWhiteSpace(startScript))
        {
            reviseStatus(BackendStatus.DISABLED);
            return;
        }
        Logs.Debug($"Requested generic launch of {startScript} on GPU {gpuId} from {nameSimple}");
        string path = startScript.Replace('\\', '/');
        string ext = path.AfterLast('.');
        if (!IsValidStartPath(nameSimple, path, ext))
        {
            reviseStatus(BackendStatus.ERRORED);
            return;
        }
        int port = NextPort++;
        ProcessStartInfo start = new()
        {
            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "./launchtools/generic-launcher.bat" : "./launchtools/generic-launcher.sh",
            RedirectStandardOutput = true,
        };
        start.ArgumentList.Add($"{gpuId}");
        start.ArgumentList.Add(Path.GetDirectoryName(path));
        start.ArgumentList.Add(path.AfterLast('/'));
        start.ArgumentList.Add(extraArgs.Replace("{PORT}", $"{port}"));
        if (startScript.EndsWith(".py"))
        {
            start.ArgumentList.Add("py");
        }
        else
        {
            start.ArgumentList.Add("shellexec");
        }
        BackendStatus status = BackendStatus.LOADING;
        reviseStatus(status);
        Process runningProcess = new() { StartInfo = start };
        takeOutput(port, runningProcess);
        runningProcess.Start();
        Logs.Init($"Self-Start {nameSimple} on port {port} is loading...");
        void MonitorLoop()
        {
            string line;
            while ((line = runningProcess.StandardOutput.ReadLine()) != null)
            {
                Logs.Debug($"{nameSimple} launcher: {line}");
            }
            status = getStatus();
            if (status == BackendStatus.RUNNING || status == BackendStatus.LOADING)
            {
                status = BackendStatus.ERRORED;
                reviseStatus(status);
            }
            Logs.Info($"Self-Start {nameSimple} on port {port} exited.");
        }
        new Thread(MonitorLoop) { Name = $"SelfStart{nameSimple}_{port}_Monitor" }.Start();
        while (status == BackendStatus.LOADING)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            Logs.Debug($"{nameSimple} port {port} checking for server...");
            bool alive = await initInternal(true);
            if (alive)
            {
                Logs.Init($"Self-Start {nameSimple} on port {port} started.");
            }
            status = getStatus();
        }
        Logs.Debug($"{nameSimple} self-start port {port} loop ending.");
    }
}
