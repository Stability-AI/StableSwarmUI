using FreneticUtilities.FreneticDataSyntax;

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

    /// <summary>Settings related to backends.</summary>
    public class BackendData : AutoConfiguration
    {

        [ConfigComment("How many times to retry initializing a backend before giving up. Default is 3.")]
        public int MaxBackendInitAttempts = 3;
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
    }

    /// <summary>Settings related to file paths.</summary>
    public class PathsData : AutoConfiguration
    {
        [ConfigComment("Root path for model files. Defaults to 'Models'")]
        public string ModelRoot = "Models";

        [ConfigComment("The model folder to use within 'ModelRoot'. Use a full-formed path (starting with '/' or a Windows drive like 'C:') to use an absolute path. Defaults to 'Stable-Diffusion'.")]
        public string SDModelFolder = "Stable-Diffusion";

        /// <summary>(Getter) Path for Stable Diffusion models.</summary>
        public string SDModelFullPath => SDModelFolder.StartsWith('/') || (SDModelFolder.Length > 2 && SDModelFolder[1] == ':') ? SDModelFolder : $"{Environment.CurrentDirectory}/{ModelRoot}/{SDModelFolder}";

        [ConfigComment("Root path for data (user configs, etc). Defaults to 'Data'")]
        public string DataPath = "Data";

        [ConfigComment("Root path for output files (images, etc). Defaults to 'Output'")]
        public string OutputPath = "Output";
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
    }

    /// <summary>Settings per-user.</summary>
    public class User : AutoConfiguration
    {
        public class OutPath : AutoConfiguration
        {
            [ConfigComment("Builder for output file paths. Can use auto-filling placeholders like '[model]' for the model name, '[prompt]' for a snippet of prompt text, etc.")]
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
            public string ImageFormat = "jpg"; // TODO: Use enum

            [ConfigComment("Whether to store metadata on saved images. Defaults enabled.")]
            public bool SaveMetadata = true;

            [ConfigComment("If set to non-0, adds DPI metadata to saved images. '72' is a good value for compatibility with some external software.")]
            public int DPI = 0;
        }

        [ConfigComment("Settings related to saved file format.")]
        public FileFormatData FileFormat = new();

        [ConfigComment("Whether your files save to server data drive or not.")]
        public bool SaveFiles = true;
    }
}
