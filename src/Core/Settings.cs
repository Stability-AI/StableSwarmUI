using FreneticUtilities.FreneticDataSyntax;
using StableSwarmUI.Backends;
using StableSwarmUI.Utils;

namespace StableSwarmUI.Core;

/// <summary>Central default settings list.</summary>
public class Settings : AutoConfiguration
{
    [ConfigComment("Settings related to file paths.")]
    public PathsData Paths = new();

    [ConfigComment("Settings related to networking and the webserver.")]
    public NetworkData Network = new();

    [ConfigComment("Restrictions to apply to default users.")]
    public UserRestriction DefaultUserRestriction = new();

    [ConfigComment("Default settings for users (unless the user modifies them, if so permitted).\n(NOTE: Usually, don't edit this. Go to the 'User' tab to edit your User-Settings).")]
    public User DefaultUser = new();

    [ConfigComment("Settings related to backends.")]
    public BackendData Backends = new();

    [ConfigComment("If this is set to 'true', hides the installer page. If 'false', the installer page will be shown.")]
    public bool IsInstalled = false;

    [ConfigComment("Ratelimit, in milliseconds, between Nvidia GPU status queries. Default is 1000 ms (1 second).")]
    public long NvidiaQueryRateLimitMS = 1000;

    [ConfigComment("How to launch the UI. If 'none', just quietly launch.\nIf 'web', launch your web-browser to the page.\nIf 'webinstall', launch web-browser to the install page.\nIf 'electron', launch the UI in an electron window (NOT YET IMPLEMENTED).")]
    [ManualSettingsOptions(Impl = null, Vals = ["none", "web", "webinstall", "electron"])]
    public string LaunchMode = "webinstall";

    [ConfigComment("If set true, some additional debugging data will be attached where relevant, such as in image metadata.")]
    public bool AddDebugData = false;

    [ConfigComment("If set true, new/upcoming/experimental features will be visible.")]
    public bool ShowExperimentalFeatures = false;

    [ConfigComment("If true, Swarm will check if there's any updates available during startup. If false, it will not check for updates.\nUpdate check only downloads a simple JSON from GitHub to get the current version info, it does not transmit any telemetry nor does it download any files or apply the update.\nDefaults to true.")]
    public bool CheckForUpdates = true;

    [ConfigComment("If true, Swarm will automatically download and apply any development version updates as soon as they're available.\nDefaults to false.")]
    public bool AutoPullDevUpdates = false;

    [ConfigComment("Settings related to logging.")]
    public LogsData Logs = new();

    [ConfigComment("Settings related to the User Interface.")]
    public UIData UI = new();

    [ConfigComment("Settings related to webhooks.")]
    public WebHooksData WebHooks = new();

    [ConfigComment("Settings related to server performance.")]
    public PerformanceData Performance = new();

    /// <summary>Settings related to logging.</summary>
    public class LogsData : AutoConfiguration
    {
        [ConfigComment("The minimum tier of logs that should be visible in the console and saved to file.\nDefault is 'info'.")]
        [SettingsOptions(Impl = typeof(SettingsOptionsAttribute.ForEnum<Logs.LogLevel>))]
        public string LogLevel = "Info";

        [ConfigComment("If true, logs will be saved to a file. If false, logs will be available in console and UI while running, but never saved to file.\nDefaults to false.\nMust restart Swarm to apply.")]
        public bool SaveLogToFile = false;

        [ConfigComment("The path for where to store log file, parsed at time of program start, relative to the Data directory.\nMust restart Swarm to apply.\nCan use [year], [month], [month_name], [day], [day_name], [hour], [minute], [second], [pid].")]
        public string LogsPath = "Logs/[year]-[month]/[day]-[hour]-[minute].log";
    }

    /// <summary>Settings related to server performance.</summary>
    public class PerformanceData : AutoConfiguration
    {
        [ConfigComment("How likely an outdated image metadata entry is to be revalidated (ie have it's mtime checked against storage) each time an image's metadata is pulled.\nDefault 0.05 means 5% chance.\nSSD users can safely set it higher. HDD users may be happier setting it to 0.\nMetadata is always loaded the first time an image is seen.")]
        public float ImageDataValidationChance = 0.05f;

        [ConfigComment("Can be enabled to cache certain backend data.\nFor example, with ComfyUI backends this will add an extended cache on the object_info data.\nDefaults to false.")]
        public bool DoBackendDataCache = false;
    }

    /// <summary>Settings related to backends.</summary>
    public class BackendData : AutoConfiguration
    {
        [ConfigComment("How many times to retry initializing a backend before giving up. Default is 3.")]
        public int MaxBackendInitAttempts = 3;

        [ConfigComment("Safety check, the maximum duration all requests can be waiting for a backend before the system declares a backend handling failure.")]
        public int MaxTimeoutMinutes = 20;

        [ConfigComment("The maximum duration an individual request can be waiting on a backend to be available before giving up.\n"
            + "Not to be confused with 'MaxTimeoutMinutes' which requires backends be unresponsive for that duration, this duration includes requests that are merely waiting because other requests are queued."
            + "\nDefaults to 60 * 24 * 7 = 1 week (ultra-long max queue duration).")]
        public int PerRequestTimeoutMinutes = 60 * 24 * 7;

        [ConfigComment("The maximum number of pending requests to continue forcing orderly processing of.\nOver this limit, requests may start going out of order.")]
        public int MaxRequestsForcedOrder = 20;

        [ConfigComment("How many minutes to wait after the last generation before automatically freeing up VRAM (to prevent issues with other programs).\nThis has the downside of a small added bit of time to load back onto VRAM at next usage.\nUse a decimal number to free after seconds.\nDefaults to 10 minutes.")]
        public double ClearVRAMAfterMinutes = 10;

        [ConfigComment("How many minutes to wait after the last generation before automatically freeing up system RAM (to prevent issues with other programs).\nThis has the downside of causing models to fully load from data drive at next usage.\nUse a decimal number to free after seconds.\nDefaults to 60 minutes (one hour).")]
        public double ClearSystemRAMAfterMinutes = 60;

        [ConfigComment("If true, any time you load the UI, trigger a server refresh.\nIf false, only triggers a refresh if you restart Swarm or trigger a refresh manually from the Quick Tools menu.\nDefaults to true.")]
        public bool AlwaysRefreshOnLoad = true;
    }

    /// <summary>Settings related to networking and the webserver.</summary>
    public class NetworkData : AutoConfiguration
    {
        [ConfigComment("What web host address to use. `localhost` means your PC only."
            + "\nLinux users may use `0.0.0.0` to mean accessible to anyone that can connect to your PC (ie LAN users, or the public if your firewall is open)."
            + "\nWindows users may use `*` for that, though it may require additional Windows firewall configuration."
            + "\nAdvanced server users may wish to manually specify a host bind address here.")]
        public string Host = "localhost";

        [ConfigComment("What web port to use. Default is '7801'.")]
        public int Port = 7801;

        [ConfigComment("If true, if the port is already in use, the server will try to find another port to use instead.\nIf false, the server will fail to start if the port is already in use.")]
        public bool PortCanChange = true;

        [ConfigComment("Backends are automatically assigned unique ports. This value selects which port number to start the assignment from.\nDefault is '7820'.")]
        public int BackendStartingPort = 7820;

        [ConfigComment("If you wish to access your Swarm instance externally, set this to the path of a CloudFlared executable, and it will automatically be used.\n(Must restart to apply).\nThe URL will be visible on the Server Info tab and/or terminal log.")]
        public string CloudflaredPath = "";
    }

    /// <summary>Settings related to file paths.</summary>
    public class PathsData : AutoConfiguration
    {
        [ConfigComment("Root path for model files. Use a full-formed path (starting with '/' or a Windows drive like 'C:') to use an absolute path.\nDefaults to 'Models'.")]
        public string ModelRoot = "Models";

        [ConfigComment("The model folder to use within 'ModelRoot'.\nDefaults to 'Stable-Diffusion'.\nAbsolute paths work too.")]
        public string SDModelFolder = "Stable-Diffusion";

        [ConfigComment("The LoRA (or related adapter type) model folder to use within 'ModelRoot'.\nDefaults to 'Lora'.\nAbsolute paths work too.")]
        public string SDLoraFolder = "Lora";

        [ConfigComment("The VAE (autoencoder) model folder to use within 'ModelRoot'.\nDefaults to 'VAE'.\nAbsolute paths work too.")]
        public string SDVAEFolder = "VAE";

        [ConfigComment("The Embedding (eg textual inversion) model folder to use within 'ModelRoot'.\nDefaults to 'Embeddings'.\nAbsolute paths work too.")]
        public string SDEmbeddingFolder = "Embeddings";

        [ConfigComment("The ControlNets model folder to use within 'ModelRoot'.\nDefaults to 'controlnet'.\nAbsolute paths work too.")]
        public string SDControlNetsFolder = "controlnet";

        [ConfigComment("The CLIP Vision model folder to use within 'ModelRoot'.\nDefaults to 'clip_vision'.\nAbsolute paths work too.")]
        public string SDClipVisionFolder = "clip_vision";

        [ConfigComment("Root path for data (user configs, etc).\nDefaults to 'Data'")]
        public string DataPath = "Data";

        [ConfigComment("Root path for output files (images, etc).\nDefaults to 'Output'")]
        public string OutputPath = "Output";

        [ConfigComment("The folder for wildcard (.txt) files, under Data.\nDefaults to 'Wildcards'")]
        public string WildcardsFolder = "Wildcards";

        [ConfigComment("When true, output paths always have the username as a folder.\nWhen false, this will be skipped.\nKeep this on in multi-user environments.")]
        public bool AppendUserNameToOutputPath = true;

        [ConfigComment("If true, model metadata is tracked on a per-folder basis. This is better for example if you copy model folders to different machines, or have symlinks to different instances, or etc.\nIf false, model metadata is tracked in the central data folder. This is better if you don't want stray files in your model folders, or if you have several Swarm instances running simultaneously.")]
        public bool ModelMetadataPerFolder = true;

        [ConfigComment("If true, image metadata is tracked on a per-folder basis.\nIf false, image metadata is tracked in the central data folder.\nThis is better if you don't want stray files in your output folders, or if you have several Swarm instances running simultaneously over the same output folders.")]
        public bool ImageMetadataPerFolder = true;
    }

    /// <summary>Settings to control restrictions on users.</summary>
    public class UserRestriction : AutoConfiguration
    {
        [ConfigComment("How many directories deep a user's custom OutPath can be.\nDefault is 5.")]
        public int MaxOutPathDepth = 5;

        [ConfigComment("Which user-settings the user is allowed to modify.\nDefault is all of them.")]
        public List<string> AllowedSettings = ["*"];

        [ConfigComment("If true, the user is treated as a full admin.\nThis includes the ability to modify these settings.")]
        public bool Admin = false;

        [ConfigComment("If true, user may load models.\nIf false, they may only use already-loaded models.")]
        public bool CanChangeModels = true;

        [ConfigComment("What models are allowed, as a path regex.\nDirectory-separator is always '/'. Can be '.*' for all, 'MyFolder/.*' for only within that folder, etc.\nDefault is all.")]
        public string AllowedModels = ".*";

        [ConfigComment("Generic permission flags. '*' means all.\nDefault is all.")]
        public List<string> PermissionFlags = ["*"];

        [ConfigComment("How many images can try to be generating at the same time on this user.")]
        public int MaxT2ISimultaneous = 32;

        /// <summary>Returns the maximum simultaneous text-2-image requests appropriate to this user's restrictions and the available backends.</summary>
        public int CalcMaxT2ISimultaneous => Math.Max(1, Math.Min(MaxT2ISimultaneous, Program.Backends.RunningBackendsOfType<AbstractT2IBackend>().Sum(b => b.MaxUsages) * 2));

        [ConfigComment("Whether the '.' symbol can be used in OutPath - if enabled, users may cause file system issues or perform folder escapes.")]
        public bool AllowUnsafeOutpaths = false;
    }

    /// <summary>Settings per-user.</summary>
    public class User : AutoConfiguration
    {
        public class OutPath : AutoConfiguration
        {
            [ConfigComment("Builder for output file paths. Can use auto-filling placeholders like '[model]' for the model name, '[prompt]' for a snippet of prompt text, etc.\n"
                + "Full details in the docs: https://github.com/Stability-AI/StableSwarmUI/blob/master/docs/User%20Settings.md#path-format")]
            public string Format = "raw/[year]-[month]-[day]/[hour][minute]-[prompt]-[model]-[seed]";

            [ConfigComment("How long any one part can be.\nDefault is 40 characters.")]
            public int MaxLenPerPart = 40;

            [ConfigComment("If true, when including model name (or loras or etc), the path will skip the folder (ie filename only).")]
            public bool ModelPathsSkipFolders = false;
        }

        [ConfigComment("Settings related to output path building.")]
        public OutPath OutPathBuilder = new();

        public class FileFormatData : AutoConfiguration
        {
            [ConfigComment("What format to save images in.\nDefault is '.png', but '.jpg' is recommended to save some filespace.")]
            [SettingsOptions(Impl = typeof(SettingsOptionsAttribute.ForEnum<Image.ImageFormat>))]
            public string ImageFormat = "PNG";

            [ConfigComment("Whether to store metadata into saved images.\nDefaults enabled.")]
            public bool SaveMetadata = true;

            [ConfigComment("If set to non-0, adds DPI metadata to saved images.\n'72' is a good value for compatibility with some external software.")]
            public int DPI = 0;

            [ConfigComment("If set to true, a '.txt' file will be saved alongside images with the image metadata easily viewable.\nThis can work even if saving in the image is disabled. Defaults disabled.")]
            public bool SaveTextFileMetadata = false;
        }

        [ConfigComment("Settings related to saved file format.")]
        public FileFormatData FileFormat = new();

        [ConfigComment("Whether your files save to server data drive or not.")]
        public bool SaveFiles = true;

        [ConfigComment("If true, folders will be discard from starred image paths.")]
        public bool StarNoFolders = false;

        public class ThemesImpl : SettingsOptionsAttribute.AbstractImpl
        {
            public override string[] GetOptions => [.. Program.Web.RegisteredThemes.Keys];

            public override string[] Names => Program.Web.RegisteredThemes.Values.Select(v => v.Name).ToArray();
        }

        [ConfigComment("What theme to use. Default is 'modern_dark'.")]
        [SettingsOptions(Impl = typeof(ThemesImpl))]
        public string Theme = "modern_dark";

        [ConfigComment("If true, images in the main center area will always grow to better fill the screen.")]
        public bool CenterImageAlwaysGrow = false;

        [ConfigComment("If true, when 'Auto Swap To Images' is enabled, and you have FullView open, the FullView will also be swapped.\nIf false, the FullView will not change.")]
        public bool AutoSwapImagesIncludesFullView = false;

        [ConfigComment("If enabled, batch size will be reset to 1 when parameters are loaded.\nThis can prevent accidents that might thrash your GPU or cause compatibility issues, especially for example when importing a comfy workflow.\nYou can still set the batch size at will in the GUI.")]
        public bool ResetBatchSizeToOne = false;

        public enum HintFormatOptions
        {
            BUTTON, HOVER, NONE
        }

        [ConfigComment("The format for parameter hints to display as.\nDefault is 'BUTTON'.")]
        [SettingsOptions(Impl = typeof(SettingsOptionsAttribute.ForEnum<HintFormatOptions>))]
        public string HintFormat = "BUTTON";

        public class VAEsData : AutoConfiguration
        {
            [ConfigComment("What VAE to use with SDXL models by default. Use 'None' to use the one in the model.")]
            [ManualSettingsOptions(Impl = null, Vals = ["None"])]
            public string DefaultSDXLVAE = "None";

            [ConfigComment("What VAE to use with SDv1 models by default. Use 'None' to use the one in the model.")]
            [ManualSettingsOptions(Impl = null, Vals = ["None"])]
            public string DefaultSDv1VAE = "None";
        }

        [ConfigComment("Options to override default VAEs with.")]
        public VAEsData VAEs = new();

        [ConfigComment("When generating live previews, this is how many simultaneous generation requests can be waiting at one time.")]
        public int MaxSimulPreviews = 1;

        [ConfigComment("Set to a number above 1 to allow generations of multiple images to automatically generate square mini-grids when they're done.")]
        public int MaxImagesInMiniGrid = 1;

        [ConfigComment("How many images the history view should stop trying to load after.")]
        public int MaxImagesInHistory = 1000;

        [ConfigComment("How many images the history view should scan server-side before deciding the list is sufficient for sorting. Not relevant when sorting by filename.")]
        public int MaxImagesScannedInHistory = 10000;

        [ConfigComment("If true, the Image History view will cache small preview thumbnails of images.\nThis should make things run faster. You can turn it off if you don't want that.")]
        public bool ImageHistoryUsePreviews = true;

        [ConfigComment("Delay, in seconds, betweeen Generate Forever updates.\nIf the delay hits and a generation is still waiting, it will be skipped.\nDefault is 0.1 seconds.")]
        public double GenerateForeverDelay = 0.1;

        public class LanguagesImpl : SettingsOptionsAttribute.AbstractImpl
        {
            public override string[] GetOptions => LanguagesHelper.SortedList;
        }

        [ConfigComment("What language to display the UI in.\nDefault is 'en' (English).")]
        [SettingsOptions(Impl = typeof(LanguagesImpl))]
        public string Language = "en";

        [ConfigComment("Comma-separated list of parameters to exclude from 'Reuse Parameters'.\nFor example, set 'model' to not copy the model, or 'model,refinermodel,videomodel' to really never copy any models.")]
        public string ReuseParamExcludeList = "";

        public class AutocompletionsImpl : SettingsOptionsAttribute.AbstractImpl
        {
            public override string[] GetOptions => ["", .. AutoCompleteListHelper.FileNames];
        }

        [ConfigComment("Optional source file for auto-completion texts (inside Data/Autocompletions).\nSee <a href=\"https://github.com/Stability-AI/StableSwarmUI/blob/master/docs/Autocompletions.md#word-lists\">docs/Autocompletions</a> for info.")]
        [SettingsOptions(Impl = typeof(AutocompletionsImpl))]
        public string AutoCompletionsSource = "";
    }

    /// <summary>UI-related settings.</summary>
    public class UIData : AutoConfiguration
    {
        [ConfigComment("Optionally specify a (raw HTML) welcome message here. If specified, will override the automatic welcome messages.")]
        public string OverrideWelcomeMessage = "";
    }

    /// <summary>Webhook settings.</summary>
    public class WebHooksData : AutoConfiguration
    {
        [ConfigComment("Webhook to call (empty JSON POST) when queues are starting up from idle.\nLeave empty to disable any webhook.\nCall must return before the first generation starts.")]
        public string QueueStartWebhook = "";

        [ConfigComment("Webhook to call (empty JSON POST) when all queues are done and the server is going idle.\nLeave empty to disable any webhook.\nCall must return before queuing may restart.")]
        public string QueueEndWebhook = "";

        [ConfigComment("How long to wait (in seconds) after all queues are done before sending the queue end webhook.\nThis is useful to prevent rapid start+end calls.")]
        public double QueueEndDelay = 1;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class SettingsOptionsAttribute : Attribute
{
    public abstract class AbstractImpl
    {
        public abstract string[] GetOptions { get; }

        public virtual string[] Names => GetOptions;
    }

    public class ForEnum<T> : AbstractImpl where T : Enum
    {
        public override string[] GetOptions => Enum.GetNames(typeof(T));
    }

    public Type Impl;

    public virtual string[] Options => (Activator.CreateInstance(Impl) as AbstractImpl).GetOptions;

    public virtual string[] Names => (Activator.CreateInstance(Impl) as AbstractImpl).Names;
}

[AttributeUsage(AttributeTargets.Field)]
public class ManualSettingsOptionsAttribute : SettingsOptionsAttribute
{
    public string[] Vals;

    public override string[] Options => Vals;

    public override string[] Names => Vals;
}
