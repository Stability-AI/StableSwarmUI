using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using LiteDB;
using StableUI.Accounts;
using StableUI.Backends;
using StableUI.Text2Image;
using StableUI.Utils;
using StableUI.WebAPI;
using System.Net.Sockets;
using System.Runtime.Loader;

namespace StableUI.Core;

/// <summary>Class that handles the core entry-point access to the program, and initialization of program layers.</summary>
public class Program
{
    /// <summary>Central store of available backends.</summary>
    public static BackendHandler Backends; // TODO: better location for central values

    /// <summary>Central store of web sessions.</summary>
    public static SessionHandler Sessions;

    /// <summary>Central store of Text2Image models.</summary>
    public static T2IModelHandler T2IModels;

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

    /// <summary>Ngrok instance, if loaded at all.</summary>
    public static NgrokHandler Ngrok;

    /// <summary>Central web server core.</summary>
    public static WebServer Web;

    /// <summary>Primary execution entry point.</summary>
    public static void Main(string[] args)
    {
        SpecialTools.Internationalize(); // Fix for MS's broken localization
        BsonMapper.Global.EmptyStringToNull = false; // Fix for LiteDB's broken handling of empty strings
        Logs.Init("=== StableUI Starting ===");
        AssemblyLoadContext.Default.Unloading += (_) => Shutdown();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
        PrepExtensions();
        try
        {
            Logs.Init("Parsing command line...");
            ParseCommandLineArgs(args);
            Logs.Init("Loading settings file...");
            SettingsFilePath = CommandLineFlags.GetValueOrDefault("settings_file", "Data/Settings.fds");
            LoadSettingsFile();
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
        RunOnAllExtensions(e => e.OnPreInit());
        Logs.Init("Prepping options...");
        T2IModels = new();
        T2IParamTypes.RegisterDefaults();
        Backends = new();
        Backends.SaveFilePath = GetCommandLineFlag("backends_file", Backends.SaveFilePath);
        Sessions = new();
        Web = new();
        RunOnAllExtensions(e => e.OnInit());
        Logs.Init("Loading models list...");
        T2IModels.Refresh();
        Logs.Init("Loading backends...");
        Backends.Load();
        Logs.Init("Prepping API...");
        BasicAPIFeatures.Register();
        foreach (string str in CommandLineFlags.Keys.Where(k => !CommandLineFlagsRead.Contains(k)))
        {
            Logs.Warning($"Unused command line flag '{str}'");
        }
        Logs.Init("Prepping webserver...");
        Web.Prep();
        RunOnAllExtensions(e => e.OnPreLaunch());
        Logs.Init("Launching server...");
        Web.Launch();
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
        Backends.Shutdown();
        Sessions.Shutdown();
        Ngrok?.Stop();
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
                Logs.Info($"Prepping extension: {extType.FullName}...");
                Extension extension = Activator.CreateInstance(extType) as Extension;
                extension.ExtensionName = extType.Name;
                Extensions.Add(extension);
                string[] possible = extType.Namespace.StartsWith("StableUI.") ? builtins : extras;
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
        string environment = GetCommandLineFlag("environment", "production").ToLowerFast() switch
        {
            "dev" or "development" => "Development",
            "prod" or "production" => "Production",
            var mode => throw new InvalidDataException($"aspweb_mode value of '{mode}' is not valid")
        };
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
        string host = GetCommandLineFlag("host", ServerSettings.Host);
        string port = GetCommandLineFlag("port", $"{ServerSettings.Port}");
        WebServer.HostURL = $"http://{host}:{port}";
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", WebServer.HostURL);
        WebServer.LogLevel = Enum.Parse<LogLevel>(GetCommandLineFlag("asp_loglevel", "warning"), true);
        Logs.MinimumLevel = Enum.Parse<Logs.LogLevel>(GetCommandLineFlag("loglevel", "info"), true);
        SessionHandler.LocalUserID = GetCommandLineFlag("user_id", SessionHandler.LocalUserID);
        LockSettings = GetCommandLineFlagAsBool("lock_settings", false);
        if (CommandLineFlags.ContainsKey("ngrok-path"))
        {
            Ngrok = new()
            {
                Path = GetCommandLineFlag("ngrok-path", null),
                Region = GetCommandLineFlag("ngrok-region", null),
                BasicAuth = GetCommandLineFlag("ngrok-basic-auth", null)
            };
        }
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
        return CommandLineFlags.GetValueOrDefault(key, def);
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
