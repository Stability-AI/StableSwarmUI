
using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;
using System.Diagnostics;
using System.IO;

namespace StableSwarmUI.Builtin_ComfyUIBackend;

public class ComfyUISelfStartBackend : ComfyUIAPIAbstractBackend
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

    public static LockObject ComfyModelFileHelperLock = new();

    public static bool IsComfyModelFileEmitted = false;

    public static void EnsureComfyFile()
    {
        lock (ComfyModelFileHelperLock)
        {
            if (IsComfyModelFileEmitted)
            {
                return;
            }
            File.WriteAllText("Data/comfy-auto-model.yaml", $"stableswarmui:\n    base_path: {Program.ServerSettings.Paths.SDModelFullPath}\n    checkpoints: ./\n");
            IsComfyModelFileEmitted = true;
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public override async Task Init()
    {
        EnsureComfyFile();
        ComfyUISelfStartSettings settings = SettingsRaw as ComfyUISelfStartSettings;
        string modelPath = $"--extra-model-paths-config {Environment.CurrentDirectory}/Data/comfy-auto-model.yaml";
        await NetworkBackendUtils.DoSelfStart(settings.StartScript, this, "ComfyUI", settings.GPU_ID, settings.ExtraArgs + " --port {PORT} " + modelPath, InitInternal, (p, r) => { Port = p; RunningProcess = r; });
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
            Logs.Info($"Shutting down self-start ComfyUI (port={Port}) process #{RunningProcess.Id}...");
            RunningProcess.Kill();
        }
        catch (Exception ex)
        {
            Logs.Error($"Error stopping ComfyUI process: {ex}");
        }
    }

    public override void PostResultCallback(string filename)
    {
        string path = (SettingsRaw as ComfyUISelfStartSettings).StartScript.Replace('\\', '/').BeforeLast('/') + "/output/" + filename;
        Task.Run(() =>
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        });
    }
}
