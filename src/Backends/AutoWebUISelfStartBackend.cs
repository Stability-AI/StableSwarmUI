using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Utils;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;

namespace StableUI.Backends;

public class AutoWebUISelfStartBackend : AutoWebUIAPIAbstractBackend<AutoWebUISelfStartBackend.AutoWebUISelfStartSettings>
{
    public class AutoWebUISelfStartSettings : AutoConfiguration
    {
        [ConfigComment("The location of the 'webui.sh' or 'webui.bat' file.")]
        public string StartScript = "";

        [ConfigComment("Any arguments to include in the launch script.")]
        public string ExtraArgs = "";

        [ConfigComment("Which GPU to use, if multiple are available.")]
        public int GPU_ID = 0; // TODO: Determine GPU count and provide correct max

        [ConfigComment("Optional delay in seconds before starting the WebUI.")]
        public int StartDelaySeconds = 0;
    }

    public Process RunningProcess;

    public static int NextPort = 7820;

    public int Port;

    public override string Address => $"http://localhost:{Port}";

    private bool IsValidStartPath(string path, string ext)
    {
        if (path.Length < 5)
        {
            return false;
        }
        if (ext != "sh" && ext != "bat")
        {
            Logs.Error($"Refusing init of {HandlerTypeData.Name} with non-script target. Please verify your start script location.");
            return false;
        }
        if (path.AfterLast('/').BeforeLast('.') == "webui-user")
        {
            Logs.Error($"Refusing init of {HandlerTypeData.Name} with 'web-ui' target script. Please use the 'webui' script instead.");
            return false;
        }
        string subPath = path[1] == ':' ? path[2..] : path;
        if (Utilities.FilePathForbidden.ContainsAnyMatch(subPath))
        {
            Logs.Error($"Failed init of {HandlerTypeData.Name} with script target '{path}' because that file path contains invalid characters ( {Utilities.FilePathForbidden.TrimToMatches(subPath)} ). Please verify your start script location.");
            return false;
        }
        if (!File.Exists(path))
        {
            Logs.Error($"Failed init of {HandlerTypeData.Name} with script target '{path}' because that file does not exist. Please verify your start script location.");
            return false;
        }
        return true;
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public override async Task Init()
    {
        if (string.IsNullOrWhiteSpace(Settings.StartScript))
        {
            Status = BackendStatus.DISABLED;
            return;
        }
        string path = Settings.StartScript.Replace('\\', '/');
        string ext = path.AfterLast('.');
        if (!IsValidStartPath(path, ext))
        {
            Status = BackendStatus.ERRORED;
            return;
        }
        Port = NextPort++;
        ProcessStartInfo start = new()
        {
            FileName = ext == "bat" ? "./launchtools/auto-webui.bat" : "./launchtools/auto-webui.sh",
            RedirectStandardOutput = true,
        };
        start.ArgumentList.Add($"{Settings.GPU_ID}");
        start.ArgumentList.Add(Path.GetDirectoryName(path));
        start.ArgumentList.Add(path);
        start.ArgumentList.Add($"{Settings.ExtraArgs} --api --port {Port}");
        Status = BackendStatus.LOADING;
        _ = Task.Run(() =>
        {
            Task.Delay(Settings.StartDelaySeconds, Program.GlobalProgramCancel).Wait();
            RunningProcess = new() { StartInfo = start };
            RunningProcess.Start();
            Logs.Init($"Self-Start WebUI on port {Port} is loading...");
            new Thread(MonitorLoop).Start();
            while (Status == BackendStatus.LOADING)
            {
                try
                {
                    Thread.Sleep(1000);
                    Logs.Debug($"Auto WebUI port {Port} checking for server...");
                    InitInternal(true).Wait();
                    if (Status == BackendStatus.RUNNING)
                    {
                        Logs.Init($"Self-Start WebUI on port {Port} started.");
                        Handler.ReassignLoadedModelsList();
                    }
                }
                catch (Exception ex)
                {
                    Logs.Error($"Error with Auto WebUI self-starter: {ex}");
                }
            }
            Logs.Debug($"Auto WebUI self-start port {Port} loop ending.");
        });
    }

    public override async Task Shutdown()
    {
        await base.Shutdown();
        try
        {
            if (RunningProcess is null || RunningProcess.HasExited)
            {
                return;
            }
            Logs.Info($"Shutting down self-start Auto WebUI (port={Port}) process #{RunningProcess.Id}...");
            RunningProcess.Kill();
        }
        catch (Exception ex)
        {
            Logs.Error($"Error stopping Auto WebUI process: {ex}");
        }
    }

    public void MonitorLoop()
    {
        string line;
        while ((line = RunningProcess.StandardOutput.ReadLine()) != null)
        {
            Logs.Debug($"Auto WebUI launcher: {line}");
        }
        Logs.Info($"Self-Start WebUI on port {Port} exited.");
    }
}
