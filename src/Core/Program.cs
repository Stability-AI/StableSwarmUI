using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using StableUI.Backends;
using StableUI.WebAPI;

namespace StableUI.Core;

/// <summary>Class that handles the core entry-point access to the program, and initialization of program layers.</summary>
public class Program
{
    /// <summary>Central store of available backends.</summary>
    public static BackendHandler Backends; // TODO: better location for central values

    /// <summary>Primary execution entry point.</summary>
    public static void Main(string[] args)
    {
        SpecialTools.Internationalize(); // Fix for MS's broken localization
        try
        {
            ParseCommandLineArgs(args);
            ApplyCoreSettings();
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine($"Command line arguments given are invalid: {ex.Message}");
            return;
        }
        Backends = new();
        BasicAPIFeatures.Register();
        WebServer.Launch();
    }

    #region settings pre-apply
    /// <summary>Pre-applies settings choices from command line (or other sources).</summary>
    public static void ApplyCoreSettings()
    {
        string environment = CommandLineFlags.GetValueOrDefault("aspweb_mode", "production").ToLowerFast() switch
        {
            "dev" or "development" => "Development",
            "prod" or "production" => "Production",
            var mode => throw new InvalidDataException($"aspweb_mode value of '{mode}' is not valid")
        };
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
        string host = CommandLineFlags.GetValueOrDefault("host", "localhost");
        string port = CommandLineFlags.GetValueOrDefault("port", "7801");
        WebServer.HostURL = $"http://{host}:{port}";
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", WebServer.HostURL);
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

    /// <summary>Command line value-flags are contained here. Flags without value contain string 'true'.</summary>
    public static Dictionary<string, string> CommandLineFlags = new();
    #endregion
}
