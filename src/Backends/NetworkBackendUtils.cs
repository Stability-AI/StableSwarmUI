using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.InteropServices;

namespace StableSwarmUI.Backends;

/// <summary>General utility for backends that self-start or use network APIs.</summary>
public static class NetworkBackendUtils
{
    #region Network
    /// <summary>Create and preconfigure a basic <see cref="HttpClient"/> instance to make web requests with.</summary>
    public static HttpClient MakeHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"StableSwarmUI/{Utilities.Version}");
        client.Timeout = TimeSpan.FromMinutes(10);
        return client;
    }

    /// <summary>Parses an <see cref="HttpResponseMessage"/> into a JSON object result.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the server returns invalid data (error code or other non-JSON).</exception>
    /// <exception cref="NotImplementedException">Thrown when an invalid JSON type is requested.</exception>
    public static async Task<JType> Parse<JType>(HttpResponseMessage message) where JType : class
    {
        string content = await message.Content.ReadAsStringAsync();
        if (content.StartsWith("500 Internal Server Error"))
        {
            throw new InvalidOperationException($"Server turned 500 Internal Server Error, something went wrong: {content}");
        }
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

    /// <summary>Connects a client websocket to the backend.</summary>
    /// <param name="path">The path to connect on, after the '/', such as 'ws?clientId={uuid}'.</param>
    public static async Task<ClientWebSocket> ConnectWebsocket(string address, string path)
    {
        ClientWebSocket outSocket = new();
        outSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        string scheme = address.BeforeAndAfter("://", out string addr);
        scheme = scheme == "http" ? "ws" : "wss";
        await outSocket.ConnectAsync(new Uri($"{scheme}://{addr}/{path}"), Program.GlobalProgramCancel);
        return outSocket;
    }

    /// <summary>Helper for API-by-URL backends to allow those backends to go idle automatically if connection is lost.</summary>
    public class IdleMonitor
    {
        public Thread IdleMonitorThread;

        public CancellationTokenSource IdleMonitorCancel = new();

        public AbstractT2IBackend Backend;

        public Action ValidateCall;

        public Action<BackendStatus> StatusChangeEvent;

        public void Start()
        {
            Stop();
            if (Backend.Status == BackendStatus.LOADING)
            {
                Backend.Status = BackendStatus.IDLE;
            }
            IdleMonitorThread = new Thread(IdleMonitorLoop);
            IdleMonitorThread.Start();
        }

        void SetStatus(BackendStatus status)
        {
            if (Backend.Status != status)
            {
                Backend.Status = status;
                StatusChangeEvent?.Invoke(status);
            }
        }

        public void IdleMonitorLoop()
        {
            CancellationToken cancel = IdleMonitorCancel.Token;
            while (true)
            {
                try
                {
                    Task.Delay(TimeSpan.FromSeconds(5), cancel).Wait();
                }
                catch (Exception)
                {
                    return;
                }
                if (cancel.IsCancellationRequested || Program.GlobalProgramCancel.IsCancellationRequested)
                {
                    return;
                }
                if (Backend.Status != BackendStatus.RUNNING && Backend.Status != BackendStatus.IDLE)
                {
                    continue;
                }
                try
                {
                    ValidateCall();
                    if (Backend.Status != BackendStatus.RUNNING && Backend.Status != BackendStatus.IDLE)
                    {
                        continue;
                    }
                    SetStatus(BackendStatus.RUNNING);
                }
                catch (Exception)
                {
                    SetStatus(BackendStatus.IDLE);
                }
            }
        }

        public void Stop()
        {
            if (IdleMonitorThread is not null)
            {
                IdleMonitorCancel.Cancel();
                IdleMonitorCancel = new();
                IdleMonitorThread = null;
            }
        }
    }
    #endregion

    #region Self Start
    /// <summary>Returns true if the path given looks valid as a start-script for a backend, or false if not with an error message explaining why.</summary>
    public static bool IsValidStartPath(string backendLabel, string path, string ext)
    {
        if (path.Length < 5)
        {
            return false;
        }
        if (ext != "sh" && ext != "bat" && ext != "py")
        {
            Logs.Error($"Refusing init of {backendLabel} with non-script target. Please verify your start script location. Path was '{path}', which does not end in the expected 'py', 'bat', or 'sh'.");
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

    /// <summary>Clean up a <see cref="ProcessStartInfo"/> environment of python env vars that cause problems.</summary>
    public static void CleanEnvironmentOfPythonMess(ProcessStartInfo start, string prefix)
    {
        void RemoveEnvLoudly(string key)
        {
            if (start.Environment.TryGetValue(key, out string val))
            {
                start.Environment.Remove(key);
                Logs.Debug($"{prefix}Removing environment variable {key} which was {val}");
            }
        }
        RemoveEnvLoudly("PYTHONHOME");
        RemoveEnvLoudly("PYTHONPATH");
        if (start.Environment.TryGetValue("LIB", out string libVal) && libVal.Contains("python"))
        {
            start.Environment.Remove("LIB");
            Logs.Debug($"{prefix}Removing environment variable LIB due to being a python-lib val which was {libVal}");
        }
        start.Environment["PYTHONUNBUFFERED"] = "true";
    }

    /// <summary>Internal tracking value of what port to use next.</summary>
    public static volatile int NextPort = 7820;

    /// <summary>Get the next available port to use, as an incremental value with checks against port usage.</summary>
    public static int GetNextPort()
    {
        int port = Interlocked.Increment(ref NextPort);
        while (Utilities.IsPortTaken(port))
        {
            port = Interlocked.Increment(ref NextPort);
        }
        return port;
    }

    /// <summary>Starts a self-start backend based on the user-configuration and backend-specifics provided.</summary>
    public static Task DoSelfStart(string startScript, AbstractT2IBackend backend, string nameSimple, int gpuId, string extraArgs, Func<bool, Task> initInternal, Action<int, Process> takeOutput)
    {
        return DoSelfStart(startScript, nameSimple, gpuId, extraArgs, status => backend.Status = status, async (b) => { await initInternal(b); return backend.Status == BackendStatus.RUNNING; }, takeOutput, () => backend.Status, a => backend.OnShutdown += a);
    }

    /// <summary>Starts a self-start backend based on the user-configuration and backend-specifics provided.</summary>
    public static async Task DoSelfStart(string startScript, string nameSimple, int gpuId, string extraArgs, Action<BackendStatus> reviseStatus, Func<bool, Task<bool>> initInternal, Action<int, Process> takeOutput, Func<BackendStatus> getStatus, Action<Action> addShutdownEvent)
    {
        if (string.IsNullOrWhiteSpace(startScript))
        {
            Logs.Debug($"Cancelling start of {nameSimple} as it has an empty start script.");
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
        int port = GetNextPort();
        string dir = Path.GetDirectoryName(path);
        ProcessStartInfo start = new()
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = dir
        };
        CleanEnvironmentOfPythonMess(start, $"({nameSimple} launch) ");
        start.Environment["CUDA_VISIBLE_DEVICES"] = $"{gpuId}";
        string preArgs = "";
        string postArgs = extraArgs.Replace("{PORT}", $"{port}").Trim();
        if (startScript.EndsWith(".py"))
        {
            preArgs = "-s " + startScript.AfterLast("/");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                void AddPath(string path)
                {
                    string above = Path.GetFullPath($"{path}/..");
                    // Strip python but be a little cautious about it
                    string[] paths = Environment.GetEnvironmentVariable("PATH").Split(';').Where(p => !p.Contains("Python3") && !p.Contains("Programs\\Python") && !p.Contains("Python\\Python")).ToArray();
                    string[] python = paths.Where(p => p.ToLowerFast().Contains("python")).ToArray();
                    if (python.Any())
                    {
                        Logs.Debug($"Python paths left: {python.JoinString("; ")}");
                    }
                    start.Environment["PATH"] = $"{path};{path}\\Scripts;{path}\\Lib;{path}\\Lib\\site-packages;{above};{paths.JoinString(";")}";
                    Logs.Debug($"({nameSimple} launch) Adding path {path}");
                }
                if (File.Exists($"{dir}/venv/Scripts/python.exe"))
                {
                    start.FileName = Path.GetFullPath($"{dir}/venv/Scripts/python.exe");
                    AddPath(Path.GetFullPath($"{dir}/venv"));
                }
                else if (File.Exists($"{dir}/../python_embeded/python.exe"))
                {
                    start.FileName = Path.GetFullPath($"{dir}/../python_embeded/python.exe");
                    start.WorkingDirectory = Path.GetFullPath($"{dir}/..");
                    preArgs = "-s " + Path.GetFullPath(startScript)[(start.WorkingDirectory.Length + 1)..];
                    AddPath(Path.GetFullPath($"{dir}/../python_embeded"));
                }
                else
                {
                    start.FileName = "python";
                }
            }
            else
            {
                void AddPath(string path)
                {
                    string above = Path.GetFullPath($"{path}/..");
                    string libFolder = Directory.GetDirectories(path, "lib").FirstOrDefault();
                    string libPath = libFolder == null ? "" : Path.GetFullPath(Utilities.CombinePathWithAbsolute(path, "lib", libFolder));
                    start.Environment["PATH"] = $"{path}:{path}/bin:{path}/lib:{libPath}:{libPath}/site-packages:{above}:{Environment.GetEnvironmentVariable("PATH")}";
                    Logs.Debug($"({nameSimple} launch) Adding path {path} and {libPath}");
                }
                if (File.Exists($"{dir}/venv/bin/python3"))
                {
                    start.FileName = Path.GetFullPath($"{dir}/venv/bin/python3");
                    AddPath(Path.GetFullPath($"{dir}/venv"));
                }
                else
                {
                    start.FileName = "python3";
                }
            }
            Logs.Debug($"({nameSimple} launch) Will use python: {start.FileName}");
        }
        else
        {
            Logs.Debug($"({nameSimple} launch) Will shellexec");
        }
        start.Arguments = $"{preArgs} {postArgs}".Trim();
        BackendStatus status = BackendStatus.LOADING;
        reviseStatus(status);
        Process runningProcess = new() { StartInfo = start };
        takeOutput(port, runningProcess);
        runningProcess.Start();
        Logs.Init($"Self-Start {nameSimple} on port {port} is loading...");
        bool isShuttingDown = false;
        addShutdownEvent?.Invoke(() => { Volatile.Write(ref isShuttingDown, true); });
        void MonitorLoop()
        {
            string line;
            bool keepShowing = false;
            while ((line = runningProcess.StandardOutput.ReadLine()) != null)
            {
                if (line.StartsWith("Traceback ("))
                {
                    keepShowing = true;
                    Logs.Warning($"{nameSimple} stdout: {line}");
                }
                else if (keepShowing && line.StartsWith("  "))
                {
                    Logs.Warning($"{nameSimple} stdout: {line}");
                }
                else if (keepShowing)
                {
                    Logs.Warning($"{nameSimple} stdout: {line}");
                    keepShowing = false;
                }
                else
                {
                    Logs.Debug($"{nameSimple} stdout: {line}");
                }
            }
            status = getStatus();
            Logs.Debug($"Status of {nameSimple} after process end is {status}");
            if (status == BackendStatus.RUNNING || status == BackendStatus.LOADING)
            {
                status = BackendStatus.ERRORED;
                reviseStatus(status);
            }
        }
        new Thread(MonitorLoop) { Name = $"SelfStart{nameSimple}_{port}_Monitor" }.Start();
        void MonitorErrLoop()
        {
            StringBuilder errorLog = new();
            string line;
            while ((line = runningProcess.StandardError.ReadLine()) != null)
            {
                Logs.Debug($"{nameSimple} stderr: {line}");
                errorLog.AppendLine($"{nameSimple} error: {line}");
                if (errorLog.Length > 1024 * 50)
                {
                    errorLog = new StringBuilder(errorLog.ToString()[(1024 * 10)..]);
                }
            }
            if (getStatus() == BackendStatus.DISABLED)
            {
                Logs.Info($"Self-Start {nameSimple} on port {port} exited properly from disabling.");
            }
            else if (Volatile.Read(ref isShuttingDown))
            {
                Logs.Info($"Self-Start {nameSimple} on port {port} exited properly.");
            }
            else
            {
                Logs.Info($"Self-Start {nameSimple} on port {port} unexpectedly exited (if something failed, launch with `--loglevel debug` to see why!)");
                if (errorLog.Length > 0)
                {
                    Logs.Info($"Self-Start {nameSimple} on port {port} had errors before shutdown:\n{errorLog}");
                }
            }
        }
        new Thread(MonitorErrLoop) { Name = $"SelfStart{nameSimple}_{port}_MonitorErr" }.Start();
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
        Logs.Debug($"{nameSimple} self-start port {port} loop ending (should now be alive)");
    }
    #endregion
}
