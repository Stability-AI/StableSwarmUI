
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
        [ConfigComment("The location of the 'main.py' file. Can be an absolute or relative path, but must end with 'main.py'.\nIf you used the installer, this should be 'dlbackend/ComfyUI/main.py'.")]
        public string StartScript = "";

        [ConfigComment("Any arguments to include in the launch script.")]
        public string ExtraArgs = "";

        [ConfigComment("If unchecked, the system will automatically add some relevant arguments to the comfy launch. If checked, automatic args (other than port) won't be added.")]
        public bool DisableInternalArgs = false;

        [ConfigComment("If checked, will automatically keep the comfy backend up to date when launching.")]
        public bool AutoUpdate = true;

        [ConfigComment("If checked, tells Comfy to generate image previews. If unchecked, previews will not be generated, and images won't show up until they're done.")]
        public bool EnablePreviews = true;

        [ConfigComment("Which GPU to use, if multiple are available.")]
        public int GPU_ID = 0; // TODO: Determine GPU count and provide correct max

        [ConfigComment("How many extra requests may queue up on this backend while one is processing.")]
        public int OverQueue = 1;

        [ConfigComment("If checked, if the backend crashes it will automatically restart.\nIf false, if the backend crashes it will sit in an errored state until manually restarted.")]
        public bool AutoRestart = true;
    }

    public Process RunningProcess;

    public int Port;

    public override string Address => $"http://localhost:{Port}";

    public override bool CanIdle => false;

    public override int OverQueue => (SettingsRaw as ComfyUISelfStartSettings).OverQueue;

    public static LockObject ComfyModelFileHelperLock = new();

    public static bool IsComfyModelFileEmitted = false;

    /// <summary>Downloads or updates the named relevant ComfyUI custom node repo.</summary>
    public static async Task<bool> EnsureNodeRepo(string url)
    {
        string nodePath = Path.GetFullPath(ComfyUIBackendExtension.Folder + "/DLNodes");
        string folderName = url.AfterLast('/');
        if (!Directory.Exists($"{nodePath}/{folderName}"))
        {
            await Process.Start(new ProcessStartInfo("git", $"clone {url}") { WorkingDirectory = nodePath }).WaitForExitAsync(Program.GlobalProgramCancel);
            string reqFile = $"{nodePath}/{folderName}/requirements.txt";
            ComfyUISelfStartBackend[] backends = [.. Program.Backends.RunningBackendsOfType<ComfyUISelfStartBackend>()];
            if (File.Exists(reqFile) && backends.Any())
            {
                Task[] tasks = [.. backends.Select(b => Program.Backends.ShutdownBackendCleanly(b.BackendData))];
                await Task.WhenAll(tasks);
                try
                {
                    Logs.Debug($"Will install requirements file {reqFile}");
                    Process p = backends.FirstOrDefault().DoPythonCall($"-s -m pip install -r {Path.GetFullPath(reqFile)}");
                    NetworkBackendUtils.ReportLogsFromProcess(p, "ComfyUI (Requirements Install)", "");
                    await p.WaitForExitAsync(Program.GlobalProgramCancel);
                }
                catch (Exception ex)
                {
                    Logs.Error($"Failed to install comfy backend node requirements: {ex}");
                }
                foreach (ComfyUISelfStartBackend backend in backends)
                {
                    Program.Backends.DoInitBackend(backend.BackendData);
                }
                return true;
            }
        }
        else
        {
            await NetworkBackendUtils.RunProcessWithMonitoring(new ProcessStartInfo("git", "pull") { WorkingDirectory = Path.GetFullPath($"{nodePath}/{folderName}") }, "comfy node pull", "comfynodepull");
        }
        return false;
    }

    public static async Task EnsureNodeRepos()
    {
        try
        {
            string nodePath = Path.GetFullPath(ComfyUIBackendExtension.Folder + "/DLNodes");
            if (!Directory.Exists(nodePath))
            {
                Directory.CreateDirectory(nodePath);
            }
            List<Task> tasks =
            [
                Task.Run(async () => await EnsureNodeRepo("https://github.com/mcmonkeyprojects/sd-dynamic-thresholding")),
                Task.Run(async () => await EnsureNodeRepo("https://github.com/Stability-AI/ComfyUI-SAI_API"))
            ];
            await Task.WhenAll(tasks);
            tasks.Clear();
            foreach (string node in Directory.EnumerateDirectories(nodePath))
            {
                if (Directory.Exists($"{node}/.git"))
                {
                    tasks.Add(NetworkBackendUtils.RunProcessWithMonitoring(new ProcessStartInfo("git", "pull") { WorkingDirectory = node }, "comfy node pull", "comfynodepull"));
                }
            }
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Logs.Error($"Failed to auto-update comfy backend node repos: {ex}");
        }
    }

    public static void EnsureComfyFile()
    {
        lock (ComfyModelFileHelperLock)
        {
            if (IsComfyModelFileEmitted)
            {
                return;
            }
            string root = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, Program.ServerSettings.Paths.ModelRoot);
            string yaml = $"""
            stableswarmui:
                base_path: {root}
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
                    upscale-models
                    upscale_models
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
                clip: |
                    clip
                unet: |
                    unet
                gligen: |
                    gligen
                ipadapter: |
                    ipadapter
                yolov8: |
                    yolov8
                tensorrt: |
                    tensorrt
                clipseg: |
                    clipseg
                custom_nodes: |
                    {Path.GetFullPath(ComfyUIBackendExtension.Folder + "/DLNodes")}
                    {Path.GetFullPath(ComfyUIBackendExtension.Folder + "/ExtraNodes")}
            """;
            Directory.CreateDirectory(Utilities.CombinePathWithAbsolute(root, Program.ServerSettings.Paths.SDClipVisionFolder));
            Directory.CreateDirectory($"{root}/upscale_models");
            File.WriteAllText($"{Program.DataDir}/comfy-auto-model.yaml", yaml);
            IsComfyModelFileEmitted = true;
        }
    }

    /// <summary>Runs a generic python call for the current comfy backend.</summary>
    public Process DoPythonCall(string call)
    {
        ProcessStartInfo start = new()
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        NetworkBackendUtils.ConfigurePythonExeFor((SettingsRaw as ComfyUISelfStartSettings).StartScript, "ComfyUI", start, out _);
        start.Arguments = call.Trim();
        return Process.Start(start);
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public override async Task Init()
    {
        EnsureComfyFile();
        string addedArgs = "";
        ComfyUISelfStartSettings settings = SettingsRaw as ComfyUISelfStartSettings;
        if (!settings.DisableInternalArgs)
        {
            string pathRaw = $"{Program.DataDir}/comfy-auto-model.yaml";
            if (pathRaw.Contains(' '))
            {
                pathRaw = $"\"{pathRaw}\"";
            }
            addedArgs += $" --extra-model-paths-config {pathRaw}";
            if (settings.EnablePreviews)
            {
                addedArgs += " --preview-method latent2rgb";
            }
        }
        settings.StartScript = settings.StartScript.Trim(' ', '"', '\'', '\n', '\r', '\t');
        if (!settings.StartScript.EndsWith("main.py") && !string.IsNullOrWhiteSpace(settings.StartScript))
        {
            Logs.Warning($"ComfyUI start script is '{settings.StartScript}', which looks wrong - did you forget to append 'main.py' on the end?");
        }
        List<Task> tasks = [Task.Run(EnsureNodeRepos)];
        if (settings.AutoUpdate && !string.IsNullOrWhiteSpace(settings.StartScript))
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    ProcessStartInfo psi = new("git", "pull")
                    {
                        WorkingDirectory = Path.GetFullPath(settings.StartScript).Replace('\\', '/').BeforeLast('/'),
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };
                    Process p = Process.Start(psi);
                    NetworkBackendUtils.ReportLogsFromProcess(p, "ComfyUI (Git Pull)", "");
                    await p.WaitForExitAsync(Program.GlobalProgramCancel);
                }
                catch (Exception ex)
                {
                    Logs.Error($"Failed to auto-update comfy backend: {ex}");
                }
            }));
        }
        await Task.WhenAll(tasks);
        string lib = NetworkBackendUtils.GetProbableLibFolderFor(settings.StartScript);
        if (lib is not null)
        {
            HashSet<string> libs = Directory.GetDirectories($"{lib}/site-packages/").Select(f => f.Replace('\\', '/').AfterLast('/').Before('-')).ToHashSet();
            async Task install(string libFolder, string pipName)
            {
                if (libs.Contains(libFolder))
                {
                    return;
                }
                Logs.Debug($"Installing '{pipName}' for ComfyUI...");
                Process p = DoPythonCall($"-s -m pip install {pipName}");
                NetworkBackendUtils.ReportLogsFromProcess(p, $"ComfyUI (Install {pipName})", "");
                await p.WaitForExitAsync(Program.GlobalProgramCancel);
            }
            await install("kornia", "kornia"); // ComfyUI added this dependency, didn't used to have it
            await install("rembg", "rembg");
            await install("matplotlib", "matplotlib");
            await install("opencv_python_headless", "opencv-python-headless");
            await install("imageio_ffmpeg", "imageio-ffmpeg");
            await install("spandrel", "spandrel");
            await install("dill", "dill");
            await install("ultralytics", "ultralytics");
            if (Directory.Exists($"{ComfyUIBackendExtension.Folder}/DLNodes/ComfyUI_IPAdapter_plus"))
            {
                // FaceID IPAdapter models need these, really inconvenient to make dependencies conditional, so...
                await install("Cython", "cython");
                if (File.Exists($"{lib}/../python311.dll"))
                {
                    // TODO: This is deeply cursed. This is published by the comfyui-ReActor-node developer so at least it's not a complete rando, but, jeesh. Insightface please fix your pip package.
                    await install("insightface", "https://github.com/Gourieff/Assets/raw/main/Insightface/insightface-0.7.3-cp311-cp311-win_amd64.whl");
                }
                else
                {
                    await install("insightface", "insightface");
                }
            }
        }
        await NetworkBackendUtils.DoSelfStart(settings.StartScript, this, $"ComfyUI-{BackendData.ID}", $"backend-{BackendData.ID}", settings.GPU_ID, settings.ExtraArgs.Trim() + " --port {PORT}" + addedArgs, InitInternal, (p, r) => { Port = p; RunningProcess = r; }, settings.AutoRestart);
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
            Utilities.KillProcess(RunningProcess, 10);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error stopping ComfyUI process: {ex}");
        }
    }

    public string ComfyPathBase => (SettingsRaw as ComfyUISelfStartSettings).StartScript.Replace('\\', '/').BeforeLast('/');

    public override void PostResultCallback(string filename)
    {
        string path =  $"{ComfyPathBase}/output/{filename}";
        Task.Run(() =>
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        });
    }

    public override bool RemoveInputFile(string filename)
    {
        string path = $"{ComfyPathBase}/input/{filename}";
        Task.Run(() =>
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        });
        return true;
    }
}
