using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Utils;
using System.Diagnostics;
using StableUI.Backends;
using static System.Net.Mime.MediaTypeNames;

namespace StableUI.Builtin_AutoWebUIExtension;

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
    }

    public Process RunningProcess;

    public static int NextPort = 7820;

    public int Port;

    public override string Address => $"http://localhost:{Port}";

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
        if (!NetworkBackendUtils.IsValidStartPath(HandlerTypeData.Name, path, ext))
        {
            Status = BackendStatus.ERRORED;
            return;
        }
        Port = NextPort++;
        ProcessStartInfo start = new()
        {
            FileName = ext == "bat" ? "./launchtools/generic-launcher.bat" : "./launchtools/generic-launcher.sh",
            RedirectStandardOutput = true,
        };
        start.ArgumentList.Add($"{Settings.GPU_ID}");
        start.ArgumentList.Add(Path.GetDirectoryName(path));
        start.ArgumentList.Add(path);
        string addedArgs = $"--api --port {Port}";
        start.ArgumentList.Add(string.IsNullOrWhiteSpace(Settings.ExtraArgs) ? addedArgs : $"{Settings.ExtraArgs} {addedArgs}");
        Status = BackendStatus.LOADING;
        RunningProcess = new() { StartInfo = start };
        RunningProcess.Start();
        Logs.Init($"Self-Start WebUI on port {Port} is loading...");
        new Thread(MonitorLoop) { Name = $"SelfStartWebUI_{Port}_Monitor" }.Start();
        while (Status == BackendStatus.LOADING)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            Logs.Debug($"Auto WebUI port {Port} checking for server...");
            InitInternal(true).Wait();
            if (Status == BackendStatus.RUNNING)
            {
                Logs.Init($"Self-Start WebUI on port {Port} started.");
            }
        }
        Logs.Debug($"Auto WebUI self-start port {Port} loop ending.");
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
        if (Status == BackendStatus.RUNNING || Status == BackendStatus.LOADING)
        {
            Status = BackendStatus.ERRORED;
        }
        Logs.Info($"Self-Start WebUI on port {Port} exited.");
    }
}
