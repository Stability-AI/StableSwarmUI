using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using LiteDB;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using StableSwarmUI.Accounts;
using StableSwarmUI.Backends;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using StableSwarmUI.WebAPI;
using System.Diagnostics;
using System.IO;
using System.Runtime.Loader;

namespace StableSwarmUI.Core;

/// <summary>Class that handles the core entry-point access to the program, and initialization of program layers.</summary>
public class Program
{
    /// <summary>Central store of available backends.</summary>
    public static BackendHandler Backends; // TODO: better location for central values

    /// <summary>Central store of web sessions.</summary>
    public static SessionHandler Sessions;

    /// <summary>Central store of Text2Image models.</summary>
    public static Dictionary<string, T2IModelHandler> T2IModelSets = new();

    /// <summary>Main Stable-Diffusion model tracker.</summary>
    public static T2IModelHandler MainSDModels => T2IModelSets["Stable-Diffusion"];

    /// <summary>All extensions currently loaded.</summary>
    public static List<Extension> Extensions = new();

    /// <summary>Holder of server admin settings.</summary>
    public static Settings ServerSettings = new();

    private static readonly CancellationTokenSource GlobalCancelSource = new();

    /// <summary>If this is signalled, the program is cancelled.</summary>
    public static CancellationToken GlobalProgramCancel = GlobalCancelSource.Token;

    /// <summary>If enabled, settings will be locked to prevent user editing.</summary>
    public static bool LockSettings = false;

    /// <summary>Path to the settings file, as set by command line.</summary>
    public static string SettingsFilePath;

    /// <summary>Proxy (ngrok/cloudflared) instance, if loaded at all.</summary>
    public static PublicProxyHandler ProxyHandler;

    /// <summary>Central web server core.</summary>
    public static WebServer Web;

    /// <summary>Event triggered when a user wants to refresh the models list.</summary>
    public static Action ModelRefreshEvent;

    /// <summary>User-requested launch mode (web, electron, none).</summary>
    public static string LaunchMode;

    /// <summary>Event-action fired when the server wasn't generating for a while and is now starting to generate again.</summary>
    public static Action TickIsGeneratingEvent;

    /// <summary>Event-action fired once per second (approximately) while the server is *not* generating anything.</summary>
    public static Action TickNoGenerationsEvent;

    /// <summary>Event-action fired once per second (approximately) all the time.</summary>
    public static Action TickEvent;

    /// <summary>General data directory root.</summary>
    public static string DataDir = "Data";

    /// <summary>Primary execution entry point.</summary>
    public static void Main(string[] args)
    {
        SpecialTools.Internationalize(); // Fix for MS's broken localization
        BsonMapper.Global.EmptyStringToNull = false; // Fix for LiteDB's broken handling of empty strings
        Logs.Init($"=== StableSwarmUI v{Utilities.Version} Starting ===");
        Utilities.LoadTimer timer = new();
        AssemblyLoadContext.Default.Unloading += (_) => Shutdown();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Logs.Debug($"Unhandled exception: {e.ExceptionObject}");
        };
        Utilities.CheckDotNet("8");
        PrepExtensions();
        try
        {
            Logs.Init("Parsing command line...");
            ParseCommandLineArgs(args);
            Logs.Init("Loading settings file...");
            DataDir = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, CommandLineFlags.GetValueOrDefault("data_dir", "Data"));
            SettingsFilePath = CommandLineFlags.GetValueOrDefault("settings_file", "Data/Settings.fds");
            LoadSettingsFile();
            // TODO: Legacy format patch from Alpha 0.5! Remove this before 1.0.
            if (ServerSettings.DefaultUser.FileFormat.ImageFormat == "jpg")
            {
                  ServerSettings.DefaultUser.FileFormat.ImageFormat = "JPG";
            }
            if (!LockSettings)
            {
                Logs.Init("Re-saving settings file...");
                SaveSettingsFile();
            }
            Logs.Init("Applying command line settings...");
            ApplyCommandLineSettings();
        }
        catch (InvalidDataException ex)
        {
            Logs.Error($"Command line arguments given are invalid: {ex.Message}");
            return;
        }
        string modelRoot = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, ServerSettings.Paths.ModelRoot);
        try
        {
            Directory.CreateDirectory(Utilities.CombinePathWithAbsolute(modelRoot, ServerSettings.Paths.SDModelFolder));
        }
        catch (IOException ex)
        {
            Logs.Error($"Failed to create directory for SD models. You may need to check your ModelRoot and SDModelFolder settings. {ex.Message}");
            return;
        }
        timer.Check("Initial settings load");
        RunOnAllExtensions(e => e.OnPreInit());
        timer.Check("Extension PreInit");
        Logs.Init("Prepping options...");
        T2IModelSets["Stable-Diffusion"] = new() { ModelType = "Stable-Diffusion", FolderPath = Utilities.CombinePathWithAbsolute(modelRoot, ServerSettings.Paths.SDModelFolder) };
        T2IModelSets["VAE"] = new() { ModelType = "VAE", FolderPath = Utilities.CombinePathWithAbsolute(modelRoot, ServerSettings.Paths.SDVAEFolder) };
        T2IModelSets["LoRA"] = new() { ModelType = "LoRA", FolderPath = Utilities.CombinePathWithAbsolute(modelRoot, ServerSettings.Paths.SDLoraFolder) };
        T2IModelSets["Embedding"] = new() { ModelType = "Embedding", FolderPath = Utilities.CombinePathWithAbsolute(modelRoot, ServerSettings.Paths.SDEmbeddingFolder) };
        T2IModelSets["ControlNet"] = new() { ModelType = "ControlNet", FolderPath = Utilities.CombinePathWithAbsolute(modelRoot, ServerSettings.Paths.SDControlNetsFolder) };
        T2IModelSets["ClipVision"] = new() { ModelType = "ClipVision", FolderPath = Utilities.CombinePathWithAbsolute(modelRoot, ServerSettings.Paths.SDClipVisionFolder) };
        T2IParamTypes.RegisterDefaults();
        Backends = new();
        Backends.SaveFilePath = GetCommandLineFlag("backends_file", Backends.SaveFilePath);
        Sessions = new();
        Web = new();
        timer.Check("Prep Objects");
        Web.PreInit();
        timer.Check("Web PreInit");
        RunOnAllExtensions(e => e.OnInit());
        timer.Check("Extensions Init");
        Utilities.PrepUtils();
        timer.Check("Prep Utils");
        LanguagesHelper.LoadAll();
        timer.Check("Languages load");
        Logs.Init("Loading models list...");
        foreach (T2IModelHandler handler in T2IModelSets.Values)
        {
            handler.Refresh();
        }
        WildcardsHelper.Init();
        timer.Check("Model listing");
        Logs.Init("Loading backends...");
        Backends.Load();
        timer.Check("Backends");
        Logs.Init("Prepping API...");
        BasicAPIFeatures.Register();
        foreach (string str in CommandLineFlags.Keys.Where(k => !CommandLineFlagsRead.Contains(k)))
        {
            Logs.Warning($"Unused command line flag '{str}'");
        }
        timer.Check("API");
        Logs.Init("Prepping webserver...");
        Web.Prep();
        timer.Check("Web prep");
        Logs.Init("Readying extensions for launch...");
        RunOnAllExtensions(e => e.OnPreLaunch());
        timer.Check("Extensions pre-launch");
        Logs.Init("Launching server...");
        Web.Launch();
        timer.Check("Web launch");
        Task.Run(() =>
        {
            Thread.Sleep(500);
            try
            {
                switch (LaunchMode.Trim().ToLowerFast())
                {
                    case "web":
                        Logs.Init("Launch web browser...");
                        Process.Start(new ProcessStartInfo(WebServer.PageURL) { UseShellExecute = true });
                        break;
                    case "webinstall":
                        Logs.Init("Launch web browser to install page...");
                        Process.Start(new ProcessStartInfo(WebServer.PageURL + "/Install") { UseShellExecute = true });
                        break;
                    case "electron":
                        Logs.Init("Electron launch not yet implemented.");
                        // TODO: Electron.NET seems not to function properly, need to get it working.
                        break;
                }
            }
            catch (Exception ex)
            {
                Logs.Error($"Failed to launch mode '{LaunchMode}' (If this is a headless/server install, change 'LaunchMode' to 'none' in settings): {ex}");
            }
        });
        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), GlobalProgramCancel);
                if (GlobalProgramCancel.IsCancellationRequested)
                {
                    return;
                }
                if (Backends.BackendsEdited)
                {
                    Backends.BackendsEdited = false;
                    Backends.Save();
                }
            }
        });
        Logs.Init("Program is running.");
        WebServer.WebApp.WaitForShutdown();
        Shutdown();
    }

    private volatile static bool HasShutdown = false;

    /// <summary>Main shutdown handler. Tells everything to stop.</summary>
    public static void Shutdown()
    {
        if (HasShutdown)
        {
            return;
        }
        HasShutdown = true;
        Logs.Info("Shutting down...");
        GlobalCancelSource.Cancel();
        WebServer.WebApp.StopAsync().Wait();
        Backends?.Shutdown();
        Sessions?.Shutdown();
        ProxyHandler?.Stop();
        foreach (T2IModelHandler handler in T2IModelSets.Values)
        {
            handler.Shutdown();
        }
        RunOnAllExtensions(e => e.OnShutdown());
        Extensions.Clear();
        ImageMetadataTracker.Shutdown();
        Logs.Info("All core shutdowns complete.");
    }

    #region extensions
    /// <summary>Initial call that prepares the extensions list.</summary>
    public static void PrepExtensions()
    {
        string[] builtins = Directory.EnumerateDirectories("./src/BuiltinExtensions").Select(s => s.Replace('\\', '/').AfterLast("/src/")).ToArray();
        string[] extras = Directory.Exists("./src/Extensions") ? Directory.EnumerateDirectories("./src/Extensions/").Select(s => s.Replace('\\', '/').AfterLast("/src/")).ToArray() : Array.Empty<string>();
        foreach (Type extType in AppDomain.CurrentDomain.GetAssemblies().ToList().SelectMany(x => x.GetTypes()).Where(t => typeof(Extension).IsAssignableFrom(t) && !t.IsAbstract))
        {
            try
            {
                Logs.Init($"Prepping extension: {extType.FullName}...");
                Extension extension = Activator.CreateInstance(extType) as Extension;
                extension.ExtensionName = extType.Name;
                Extensions.Add(extension);
                string[] possible = extType.Namespace.StartsWith("StableSwarmUI.") ? builtins : extras;
                foreach (string path in possible)
                {
                    if (File.Exists($"src/{path}/{extType.Name}.cs"))
                    {
                        if (extension.FilePath is not null)
                        {
                            Logs.Error($"Multiple extensions with the same name {extType.Name}! Something will break.");
                        }
                        extension.FilePath = $"src/{path}/";
                    }
                }
                if (extension.FilePath is null)
                {
                    Logs.Error($"Could not determine path for extension {extType.Name} - is the classname mismatched from the filename? Searched in {string.Join(", ", possible)} for '{extType.Name}.cs'");
                }
            }
            catch (Exception ex)
            {
                Logs.Error($"Failed to create extension of type {extType.FullName}: {ex}");
            }
        }
        RunOnAllExtensions(e => e.OnFirstInit());
    }

    /// <summary>Runs an action on all extensions.</summary>
    public static void RunOnAllExtensions(Action<Extension> action)
    {
        foreach (Extension ext in Extensions)
        {
            try
            {
                action(ext);
            }
            catch (Exception ex)
            {
                Logs.Error($"Failed to run event on extension {ext.GetType().FullName}: {ex}");
            }
        }
    }

    /// <summary>Returns the extension instance of the given type.</summary>
    public static T GetExtension<T>() where T : Extension
    {
        return Extensions.FirstOrDefault(e => e is T) as T;
    }
    #endregion

    #region settings
    /// <summary>Load the settings file.</summary>
    public static void LoadSettingsFile()
    {
        FDSSection section;
        try
        {
            section = FDSUtility.ReadFile(SettingsFilePath);
        }
        catch (FileNotFoundException)
        {
            Logs.Init("No settings file found.");
            return;
        }
        catch (Exception ex)
        {
            Logs.Error($"Error loading settings file: {ex}");
            return;
        }
        ServerSettings.Load(section);
    }

    /// <summary>Save the settings file.</summary>
    public static void SaveSettingsFile()
    {
        if (LockSettings)
        {
            return;
        }
        try
        {
            FDSUtility.SaveToFile(ServerSettings.Save(true), SettingsFilePath);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error saving settings file: {ex}");
            return;
        }
    }
    #endregion

    #region command-line pre-apply
    /// <summary>Pre-applies settings choices from command line.</summary>
    public static void ApplyCommandLineSettings()
    {
        ReapplySettings();
        string environment = GetCommandLineFlag("environment", "production").ToLowerFast() switch
        {
            "dev" or "development" => "Development",
            "prod" or "production" => "Production",
            var mode => throw new InvalidDataException($"aspweb_mode value of '{mode}' is not valid")
        };
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
        string host = GetCommandLineFlag("host", ServerSettings.Network.Host);
        int port = int.Parse(GetCommandLineFlag("port", $"{ServerSettings.Network.Port}"));
        if (new int[] { 21, 22, 80, 8080, 7860, 8188 }.Contains(port))
        {
            Logs.Warning($"Port {port} looks like a port commonly used by other programs. You may want to change it.");
        }
        if (ServerSettings.Network.PortCanChange)
        {
            int origPort = port;
            while (Utilities.IsPortTaken(port))
            {
                port++;
            }
            if (origPort != port)
            {
                Logs.Init($"Port {origPort} was taken, using {port} instead.");
            }
        }
        WebServer.SetHost(host, port);
        NetworkBackendUtils.NextPort = ServerSettings.Network.BackendStartingPort;
        if (NetworkBackendUtils.NextPort < 1000)
        {
              Logs.Warning($"BackendStartingPort setting {NetworkBackendUtils.NextPort} is a low-range value (below 1000), which may cause it to conflict with the OS or other programs. You may want to change it.");
        }
        WebServer.LogLevel = Enum.Parse<LogLevel>(GetCommandLineFlag("asp_loglevel", "warning"), true);
        SessionHandler.LocalUserID = GetCommandLineFlag("user_id", SessionHandler.LocalUserID);
        LockSettings = GetCommandLineFlagAsBool("lock_settings", false);
        if (CommandLineFlags.ContainsKey("ngrok-path"))
        {
            ProxyHandler = new()
            {
                Name = "Ngrok",
                Path = GetCommandLineFlag("ngrok-path", null),
                Region = GetCommandLineFlag("proxy-region", null),
                BasicAuth = GetCommandLineFlag("ngrok-basic-auth", null)
            };
        }
        if (CommandLineFlags.ContainsKey("cloudflared-path"))
        {
            ProxyHandler = new()
            {
                Name = "Cloudflare",
                Path = GetCommandLineFlag("cloudflared-path", null),
                Region = GetCommandLineFlag("proxy-region", null)
            };
        }
        LaunchMode = GetCommandLineFlag("launch_mode", ServerSettings.LaunchMode);
    }

    /// <summary>Applies runtime-changable settings.</summary>
    public static void ReapplySettings()
    {
        Logs.MinimumLevel = Enum.Parse<Logs.LogLevel>(GetCommandLineFlag("loglevel", ServerSettings.LogLevel), true);
    }
    #endregion

    #region command line
    /// <summary>Parses command line argument inputs and splits them into <see cref="CommandLineFlags"/> and <see cref="CommandLineValueFlags"/>.</summary>
    public static void ParseCommandLineArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--"))
            {
                throw new InvalidDataException($"Error: Unknown command line argument '{arg}'");
            }
            string key = arg[2..].ToLower();
            string value = "true";
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            {
                value = args[++i];
            }
            if (CommandLineFlags.ContainsKey(key))
            {
                throw new InvalidDataException($"Error: Duplicate command line flag '{key}'");
            }
            CommandLineFlags[key] = value;
        }
    }

    /// <summary>Command line value-flags are contained here. Flags without value contain string 'true'. Don't read this directly, use <see cref="GetCommandLineFlag(string, string)"/>.</summary>
    public static Dictionary<string, string> CommandLineFlags = new();

    /// <summary>Helper to identify when command line flags go unused.</summary>
    public static HashSet<string> CommandLineFlagsRead = new();

    /// <summary>Get the command line flag for a given name, and default value.</summary>
    public static string GetCommandLineFlag(string key, string def)
    {
        CommandLineFlagsRead.Add(key);
        if (CommandLineFlags.TryGetValue(key, out string value))
        {
            return value;
        }
        if (key.Contains('_'))
        {
            return GetCommandLineFlag(key.Replace("_", ""), def);
        }
        return def;
    }

    /// <summary>Gets the command line flag for the given key as a boolean.</summary>
    public static bool GetCommandLineFlagAsBool(string key, bool def)
    {
        return GetCommandLineFlag(key, def.ToString()).ToLowerFast() switch
        {
            "true" or "yes" or "1" => true,
            "false" or "no" or "0" => false,
            var mode => throw new InvalidDataException($"Command line flag '{key}' value of '{mode}' is not valid")
        };
    }
    #endregion
}
