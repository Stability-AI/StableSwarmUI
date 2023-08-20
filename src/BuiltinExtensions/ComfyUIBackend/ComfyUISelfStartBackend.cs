
using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace StableSwarmUI.Builtin_ComfyUIBackend;

public class ComfyUISelfStartBackend : ComfyUIAPIAbstractBackend
{
    public class ComfyUISelfStartSettings : AutoConfiguration
    {
        [ConfigComment("The location of the 'main.py' file. Can be an absolute or relative path, but must end with 'main.py'.\nIf you used the installer, this should be 'dlbackend/ComfyUI/main.py'.")]
        public string StartScript = "";

        [ConfigComment("Any arguments to include in the launch script.")]
        public string ExtraArgs = "";

        [ConfigComment("If unchecked, the system will automatically add some relevant arguments to the comfy launch. If checked, automatic args (other than port) won't be added.")]
        public bool DisableInternalArgs = false;

        [ConfigComment("If enabled, will automatically keep the comfy backend up to date when launching.")]
        public bool AutoUpdate = true;

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
            string yaml = $"""
            stableswarmui:
                base_path: {Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, Program.ServerSettings.Paths.ModelRoot)}
                checkpoints: {Program.ServerSettings.Paths.SDModelFolder}
                vae: |
                    {Program.ServerSettings.Paths.SDVAEFolder}
                    VAE
                loras: |
                    {Program.ServerSettings.Paths.SDLoraFolder}
                    Lora
                    LyCORIS
                upscale_models: |
                    ESRGAN
                    RealESRGAN
                    SwinIR
                embeddings: |
                    {Program.ServerSettings.Paths.SDEmbeddingFolder}
                    embeddings
                hypernetworks: hypernetworks
                controlnet: |
                    {Program.ServerSettings.Paths.SDControlNetsFolder}
                    ControlNet
                clip_vision: |
                    {Program.ServerSettings.Paths.SDClipVisionFolder}
                    clip_vision
            """;
            File.WriteAllText("Data/comfy-auto-model.yaml", yaml);
            IsComfyModelFileEmitted = true;
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public override async Task Init()
    {
        EnsureComfyFile();
        string addedArgs = "";
        ComfyUISelfStartSettings settings = SettingsRaw as ComfyUISelfStartSettings;
        if (!settings.DisableInternalArgs)
        {
            string pathRaw = $"{Environment.CurrentDirectory}/Data/comfy-auto-model.yaml";
            if (pathRaw.Contains(' '))
            {
                pathRaw = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"'{pathRaw}'" : $"\"{pathRaw}\"";
            }
            string modelPath = $"--extra-model-paths-config {pathRaw}";
            addedArgs = $" --preview-method auto {modelPath}";
        }
        if (!settings.StartScript.EndsWith("main.py"))
        {
            Logs.Warning($"ComfyUI start script is '{settings.StartScript}', which looks wrong - did you forget to append 'main.py' on the end?");
        }
        if (settings.AutoUpdate)
        {
            try
            {
                ProcessStartInfo psi = new("git", "pull") { WorkingDirectory = Path.GetFullPath(settings.StartScript).Replace('\\', '/').BeforeLast('/') };
                Process p = Process.Start(psi);
                await p.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                Logs.Error($"Failed to auto-update comfy backend: {ex}");
            }
        }
        await NetworkBackendUtils.DoSelfStart(settings.StartScript, this, "ComfyUI", settings.GPU_ID, settings.ExtraArgs.Trim() + " --port {PORT}" + addedArgs, InitInternal, (p, r) => { Port = p; RunningProcess = r; });
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
