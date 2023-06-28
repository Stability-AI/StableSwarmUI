
using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using StableUI.Backends;
using StableUI.Utils;
using System.Diagnostics;
using System.IO;

namespace StableUI.Builtin_ComfyUIBackend;

public class ComfyUISelfStartBackend : ComfyUIAPIAbstractBackend<ComfyUISelfStartBackend.ComfyUISelfStartSettings>
{
    public class ComfyUISelfStartSettings : AutoConfiguration
    {
        [ConfigComment("The location of the 'main.py' file.")]
        public string StartScript = "";

        [ConfigComment("Any arguments to include in the launch script.")]
        public string ExtraArgs = "";

        [ConfigComment("Which GPU to use, if multiple are available.")]
        public int GPU_ID = 0; // TODO: Determine GPU count and provide correct max
    }

    public Process RunningProcess;

    public int Port;

    public override string Address => $"http://localhost:{Port}";

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public override async Task Init()
    {
        await NetworkBackendUtils.DoSelfStart(Settings.StartScript, this, "ComfyUI", Settings.GPU_ID, Settings.ExtraArgs + " --port {PORT}", InitInternal, (p, r) => { Port = p; RunningProcess = r; });
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

    public override void PostResultCallback(string filename)
    {
        string path = Settings.StartScript.Replace('\\', '/').BeforeLast('/') + "/output/" + filename;
        Task.Run(() =>
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        });
    }
}
