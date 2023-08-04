using FreneticUtilities.FreneticDataSyntax;
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

    [ConfigComment("Default settings for users (unless the user modifies them, if so permitted).")]
    public User DefaultUser = new();

    [ConfigComment("Settings related to backends.")]
    public BackendData Backends = new();

    [ConfigComment("If this is set to 'true', hides the installer page. If 'false', the installer page will be shown.")]
    public bool IsInstalled = false;

    [ConfigComment("Ratelimit, in milliseconds, between Nvidia GPU status queries. Default is 1000 ms (1 second).")]
    public long NvidiaQueryRateLimitMS = 1000;

    [ConfigComment("How to launch the UI. If 'none', just quietly launch. If 'web', launch your web-browser to the page. If 'webinstall', launch web-browser to the install page. If 'electron', launch the UI in an electron window.")]
    public string LaunchMode = "webinstall";

    /// <summary>Settings related to backends.</summary>
    public class BackendData : AutoConfiguration
    {

        [ConfigComment("How many times to retry initializing a backend before giving up. Default is 3.")]
        public int MaxBackendInitAttempts = 3;

        [ConfigComment("The maximum duration a request can be waiting on a backend to be available before giving up.")]
        public int MaxTimeoutMinutes = 20;
    }

    /// <summary>Settings related to networking and the webserver.</summary>
    public class NetworkData : AutoConfiguration
    {
        [ConfigComment("What web host address to use. `localhost` means your PC only."
            + " Linux users may use `0.0.0.0` to mean accessible to anyone that can connect to your PC (ie LAN users, or the public if your firewall is open)."
            + " Windows users may use `*` for that, though it may require additional Windows firewall configuration."
            + " Advanced server users may wish to manually specify a host bind address here.")]
        public string Host = "localhost";

        [ConfigComment("What web port to use. Default is '7801'.")]
        public int Port = 7801;

        [ConfigComment("If true, if the port is already in use, the server will try to find another port to use instead. If false, the server will fail to start if the port is already in use.")]
        public bool PortCanChange = true;
    }

    /// <summary>Settings related to file paths.</summary>
    public class PathsData : AutoConfiguration
    {
        [ConfigComment("Root path for model files. Use a full-formed path (starting with '/' or a Windows drive like 'C:') to use an absolute path. Defaults to 'Models'.")]
        public string ModelRoot = "Models";

        [ConfigComment("The model folder to use within 'ModelRoot'. Defaults to 'Stable-Diffusion'.")]
        public string SDModelFolder = "Stable-Diffusion";

        [ConfigComment("The LoRA (or related adapter type) model folder to use within 'ModelRoot'. Defaults to 'Lora'.")]
        public string SDLoraFolder = "Lora";

        [ConfigComment("The VAE (autoencoder) model folder to use within 'ModelRoot'. Defaults to 'VAE'.")]
        public string SDVAEFolder = "VAE";

        [ConfigComment("The Embedding (eg textual inversion) model folder to use within 'ModelRoot'. Defaults to 'Embeddings'.")]
        public string SDEmbeddingFolder = "VAE";

        [ConfigComment("Root path for data (user configs, etc). Defaults to 'Data'")]
        public string DataPath = "Data";

        [ConfigComment("Root path for output files (images, etc). Defaults to 'Output'")]
        public string OutputPath = "Output";

        [ConfigComment("When true, output paths always have the username as a folder. When false, this will be skipped. Keep this on in multi-user environments.")]
        public bool AppendUserNameToOutputPath = true;
    }

    /// <summary>Settings to control restrictions on users.</summary>
    public class UserRestriction : AutoConfiguration
    {
        [ConfigComment("How many directories deep a user's custom OutPath can be. Default is 5.")]
        public int MaxOutPathDepth = 5;

        [ConfigComment("Which user-settings the user is allowed to modify. Default is all of them.")]
        public List<string> AllowedSettings = new() { "*" };

        [ConfigComment("If true, the user is treated as a full admin. This includes the ability to modify these settings.")]
        public bool Admin = false;

        [ConfigComment("If true, user may load models. If false, they may only use already-loaded models.")]
        public bool CanChangeModels = true;

        [ConfigComment("What models are allowed, as a path regex. Directory-separator is always '/'. Can be '.*' for all, 'MyFolder/.*' for only within that folder, etc. Default is all.")]
        public string AllowedModels = ".*";

        [ConfigComment("Generic permission flags. '*' means all. Default is all.")]
        public List<string> PermissionFlags = new() { "*" };

        [ConfigComment("How many images can try to be generating at the same time on this user.")]
        public int MaxT2ISimultaneous = 32;

        /// <summary>Returns the maximum simultaneous text-2-image requests appropriate to this user's restrictions and the available backends.</summary>
        public int CalcMaxT2ISimultaneous => Math.Max(1, Math.Min(MaxT2ISimultaneous, Program.Backends.Count * 2));
    }

    /// <summary>Settings per-user.</summary>
    public class User : AutoConfiguration
    {
        public class OutPath : AutoConfiguration
        {
            [ConfigComment("Builder for output file paths. Can use auto-filling placeholders like '[model]' for the model name, '[prompt]' for a snippet of prompt text, etc.\n"
                + "Full details in the docs: https://github.com/Stability-AI/StableSwarmUI/blob/master/docs/User%20Settings.md#path-format")]
            // TODO: Docs link that documents full set of options here.
            public string Format = "raw/[year]-[month]-[day]/[prompt]-[model]-[seed]";

            [ConfigComment("How long any one part can be. Default is 40 characters.")]
            public int MaxLenPerPart = 40;
        }

        [ConfigComment("Settings related to output path building.")]
        public OutPath OutPathBuilder = new();

        public class FileFormatData : AutoConfiguration
        {
            [ConfigComment("What format to save images in. Default is '.jpg' (at 100% quality).")]
            [SettingsOptions(Impl = typeof(SettingsOptionsAttribute.ForEnum<Image.ImageFormat>))]
            public string ImageFormat = "JPG";

            [ConfigComment("Whether to store metadata into saved images. Defaults enabled.")]
            public bool SaveMetadata = true;

            [ConfigComment("If set to non-0, adds DPI metadata to saved images. '72' is a good value for compatibility with some external software.")]
            public int DPI = 0;

            [ConfigComment("If set to true, a '.txt' file will be saved alongside images with the image metadata easily viewable. This can work even if saving in the image is disabled. Defaults disabled.")]
            public bool SaveTextFileMetadata = false;
        }

        [ConfigComment("Settings related to saved file format.")]
        public FileFormatData FileFormat = new();

        [ConfigComment("Whether your files save to server data drive or not.")]
        public bool SaveFiles = true;

        public class ThemesImpl : SettingsOptionsAttribute.AbstractImpl
        {
            public override string[] GetOptions => Program.Web.RegisteredThemes.Keys.ToArray();
        }

        [ConfigComment("What theme to use. Default is 'dark_dreams'.")]
        [SettingsOptions(Impl = typeof(ThemesImpl))]
        public string Theme = "dark_dreams";
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class SettingsOptionsAttribute : Attribute
{
    public abstract class AbstractImpl
    {
        public abstract string[] GetOptions { get; }
    }

    public class ForEnum<T> : AbstractImpl where T : Enum
    {
        public override string[] GetOptions => Enum.GetNames(typeof(T));
    }

    public Type Impl;

    public string[] Options => (Activator.CreateInstance(Impl) as AbstractImpl).GetOptions;
}
