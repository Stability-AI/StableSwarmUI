using FreneticUtilities.FreneticDataSyntax;

namespace StableUI.Core;

/// <summary>Central default settings list.</summary>
public class Settings : AutoConfiguration
{
    [ConfigComment("Root path for model files. Defaults to (your dir)/Models")]
    public string ModelRoot = "Models";

    /// <summary>(Getter) Path for Stable Diffusion models.</summary>
    public string SDModelFullPath => $"{Environment.CurrentDirectory}/{ModelRoot}/Stable-Diffusion";

    [ConfigComment("Root path for data (user configs, etc). Defaults to (your dir)/Data")]
    public string DataPath = "Data";

    [ConfigComment("Root path for output files (images, etc). Defaults to (your dir)/Output")]
    public string OutputPath = "Output";

    [ConfigComment("What web host address to use, `localhost` means your PC only,"
        + " `*` means accessible to anyone that can connect to your PC (ie LAN users, or the public if your firewall is open)."
        + " Advanced server users may wish to manually specify a host bind address here.")]
    public string Host = "localhost";

    [ConfigComment("What web port to use. Default is '7801'.")]
    public int Port = 7801;

    [ConfigComment("Restrictions to apply to default users.")]
    public UserRestriction DefaultUserRestriction = new();

    [ConfigComment("Default settings for users (unless the user modifies them, if so permitted).")]
    public User DefaultUser = new();

    /// <summary>Settings to control restrictions on users.</summary>
    public class UserRestriction : AutoConfiguration
    {
        [ConfigComment("How many directories deep a user's custom OutPath can be. Default is 5.")]
        public int MaxOutPathDepth = 5;

        [ConfigComment("Which user-settings the user is allowed to modify. Default is all of them.")]
        public List<string> AllowedSettings = new() { "*" };

        [ConfigComment("If true, the user is treated as a full admin. This includes the ability to modify these settings.")]
        public bool Admin = false;
    }

    /// <summary>Settings per-user.</summary>
    public class User : AutoConfiguration
    {
        public class OutPath : AutoConfiguration
        {
            [ConfigComment("Builder for output file paths. Can use auto-filling placeholders like '[model]' for the model name, '[prompt]' for a snippet of prompt text, etc.")]
            public string Format = "[model]/[prompt]/[seed]";

            [ConfigComment("How long any one part can be. Default is 40 characters.")]
            public int MaxLenPerPart = 40;
        }

        [ConfigComment("Settings related to output path building.")]
        public OutPath OutPathBuilder = new();

        [ConfigComment("Whether your files save to server data drive or not.")]
        public bool SaveFiles = true;

        [ConfigComment("What format to save images in. Default is '.png'.")] // TODO: Enum/whitelist of valid options?
        public string ImageFormat = "png";

        [ConfigComment("Whether to store metadata on saved images. Defaults enabled.")]
        public bool SaveMetadata = true;
    }
}
