using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableUI.Accounts;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Utils;
using System.IO;

namespace StableUI.Text2Image;

/// <summary>Represents the data-type of a Text2Image parameter type.</summary>
public enum T2IParamDataType
{
    /// <summary>Raw text input.</summary>
    TEXT,
    /// <summary>Integer input (number without decimal).</summary>
    INTEGER,
    /// <summary>Number with decimal input.</summary>
    DECIMAL,
    /// <summary>Input is just 'true' or 'false'; a checkbox.</summary>
    BOOLEAN,
    /// <summary>Selection explicitly from a list.</summary>
    DROPDOWN,
    /// <summary>Image file input.</summary>
    IMAGE
}

/// <summary>Which format to display a number in.</summary>
public enum NumberViewType
{
    /// <summary>Small numeric input box.</summary>
    SMALL,
    /// <summary>Large numeric input box.</summary>
    BIG,
    /// <summary>Ordinary range slider.</summary>
    SLIDER,
    /// <summary>Power-of-Two slider, used especially for Width/Height of an image.</summary>
    POT_SLIDER
}

/// <summary>
/// Defines a parameter type for Text2Image, in full, for usage in the UI and more.
/// </summary>
/// <param name="Name">The (full, proper) name of the parameter.</param>
/// <param name="Description">A user-friendly description text of what the parameter is/does.</param>
/// <param name="Type">The type of the type - text vs integer vs etc.</param>
/// <param name="Default">A default value for this parameter.</param>
/// <param name="Apply">A function that applies a parameter value to a <see cref="T2IParams"/> instance.</param>
/// <param name="Min">(For numeric types) the minimum value.</param>
/// <param name="Max">(For numeric types) the maximum value.</param>
/// <param name="Step">(For numeric types) the step rate for UI usage.</param>
/// <param name="Clean">An optional special method to clean up text input.</param>
/// <param name="GetValues">A method that returns a list of valid values, for input validation.</param>
/// <param name="Examples">A set of example values to be visible in some UIs.</param>
/// <param name="ParseList">An optional special method to clean up a list of text inputs.</param>
/// <param name="ValidateValues">If set to false, prevents the normal validation of the 'Values' list.</param>
/// <param name="VisibleNormally">Whether the parameter should be visible in the main UI.</param>
/// <param name="IsAdvanced">If 'false', this is an advanced setting that should be hidden by a dropdown.</param>
/// <param name="FeatureFlag">If set, this parameter is only available when backends or models provide the given feature flag.</param>
/// <param name="Permission">If set, users must have the given permission flag to use this parameter.</param>
/// <param name="Toggleable">If true, the setting's presence can be toggled on/off.</param>
/// <param name="OrderPriority">Value to help sort parameter types appropriately.</param>
/// <param name="Group">Optional grouping info.</param>
/// <param name="NumberView">How to display a number input.</param>
/// 
public record class T2IParamType(string Name, string Description, T2IParamDataType Type, string Default, Action<string, T2IParams> Apply, double Min = 0, double Max = 0, double Step = 1,
    Func<string, string> Clean = null, Func<Session, List<string>> GetValues = null, string[] Examples = null, Func<List<string>, List<string>> ParseList = null, bool ValidateValues = true,
    bool VisibleNormally = true, bool IsAdvanced = false, string FeatureFlag = null, string Permission = null, bool Toggleable = false, double OrderPriority = 10, T2IParamGroup Group = null,
    NumberViewType NumberView = NumberViewType.SMALL)
{
    public JObject ToNet(Session session)
    {
        return new JObject()
        {
            ["name"] = Name,
            ["id"] = T2IParamTypes.CleanTypeName(Name),
            ["description"] = Description,
            ["type"] = Type.ToString().ToLowerFast(),
            ["default"] = Default,
            ["min"] = Min,
            ["max"] = Max,
            ["step"] = Step,
            ["values"] = GetValues == null ? null : JToken.FromObject(GetValues(session)),
            ["examples"] = Examples == null ? null : JToken.FromObject(Examples),
            ["visible"] = VisibleNormally,
            ["advanced"] = IsAdvanced,
            ["feature_flag"] = FeatureFlag,
            ["toggleable"] = Toggleable,
            ["priority"] = OrderPriority,
            ["group"] = Group?.ToNet(session),
            ["number_view_type"] = NumberView.ToString().ToLowerFast()
        };
    }
}

/// <summary>Represents a group of parameters.</summary>
/// <param name="Name">The name of the group.</param>
/// <param name="Toggles">If true, the entire group toggles as one.</param>
/// <param name="Open">If true, the group defaults open. If false, it defaults to closed.</param>
/// <param name="OrderPriority">The priority order position to put this group in.</param>
/// <param name="IsAdvanced">If 'false', this is an advanced setting group that should be hidden by a dropdown.</param>
public record class T2IParamGroup(string Name, bool Toggles = false, bool Open = true, double OrderPriority = 10, bool IsAdvanced = false)
{
    public JObject ToNet(Session session)
    {
        return new JObject()
        {
            ["name"] = Name,
            ["id"] = T2IParamTypes.CleanTypeName(Name),
            ["toggles"] = Toggles,
            ["open"] = Open,
            ["priority"] = OrderPriority,
            ["advanced"] = IsAdvanced
        };
    }
}

/// <summary>Central manager of Text2Image parameter types.</summary>
public class T2IParamTypes
{
    /// <summary>Map of all currently loaded types, by cleaned name.</summary>
    public static Dictionary<string, T2IParamType> Types = new();

    /// <summary>Helper to match valid text for use in a parameter type name.</summary>
    public static AsciiMatcher CleanTypeNameMatcher = new(AsciiMatcher.LowercaseLetters);

    /// <summary>Register a new parameter type.</summary>
    public static void Register(T2IParamType type)
    {

        Types.Add(CleanTypeName(type.Name), type);
    }

    /// <summary>Type-name cleaner.</summary>
    public static string CleanTypeName(string name)
    {
        return CleanTypeNameMatcher.TrimToMatches(name.ToLowerFast().Trim());
    }

    /// <summary>Generic user-input name cleaner.</summary>
    public static string CleanNameGeneric(string name)
    {
        return name.ToLowerFast().Replace(" ", "").Replace("[", "").Replace("]", "").Trim();
    }

    /// <summary>Applies a string edit, with support for "{value}" notation.</summary>
    public static string ApplyStringEdit(string prior, string update)
    {
        if (update.Contains("{value}"))
        {
            return update.Replace("{value}", prior ?? "");
        }
        return update;
    }

    /// <summary>(Called by <see cref="Program"/> during startup) registers all default parameter types.</summary>
    public static void RegisterDefaults()
    {
        Register(new("Prompt", "The input prompt text that describes the image you want to generate.\nTell the AI what you want to see.",
            T2IParamDataType.TEXT, "", (s, p) => p.Prompt = ApplyStringEdit(p.Prompt, s), Examples: new[] { "a photo of a cat", "a cartoonish drawing of an astronaut" }, OrderPriority: -100
            ));
        Register(new("Negative Prompt", "Like the input prompt text, but describe what NOT to generate.\nTell the AI things you don't want to see.",
            T2IParamDataType.TEXT, "", (s, p) => p.NegativePrompt = ApplyStringEdit(p.NegativePrompt, s), Examples: new[] { "ugly, bad, gross", "lowres, low quality" }, OrderPriority: -90
            ));
        T2IParamGroup coreGroup = new("Core Parameters", Toggles: false, Open: true, OrderPriority: -50);
        Register(new("Images", "How many images to generate at once.",
            T2IParamDataType.INTEGER, "1", (s, p) => { }, Min: 1, Max: 100, Step: 1, Examples: new[] { "1", "4" }, OrderPriority: -50, Group: coreGroup
            ));
        Register(new("Steps", "How many times to run the model.\nMore steps = better quality, but more time.\n20 is a good baseline for speed, 40 is good for maximizing quality.\nYou can go much higher, but it quickly becomes pointless above 70 or so.",
            T2IParamDataType.INTEGER, "20", (s, p) => p.Steps = int.Parse(s), Min: 1, Max: 200, Step: 1, Examples: new[] { "10", "15", "20", "30", "40" }, OrderPriority: -20, Group: coreGroup
            ));
        Register(new("Seed", "Image seed.\n-1 = random.",
            T2IParamDataType.INTEGER, "-1", (s, p) => p.Seed = int.Parse(s), Min: -1, Max: uint.MaxValue, Step: 1, Examples: new[] { "1", "2", "...", "10" }, OrderPriority: -19, NumberView: NumberViewType.BIG, Group: coreGroup
            ));
        Register(new("CFG Scale", "How strongly to scale prompt input.\nToo-high values can cause corrupted/burnt images, too-low can cause nonsensical images.\n7 is a good baseline. Normal usages vary between 5 and 9.",
            T2IParamDataType.DECIMAL, "7", (s, p) => p.CFGScale = float.Parse(s), Min: 0, Max: 30, Step: 0.25, Examples: new[] { "5", "6", "7", "8", "9" }, OrderPriority: -18, NumberView: NumberViewType.SLIDER, Group: coreGroup
            ));
        T2IParamGroup variationGroup = new("Variation Seed", Toggles: false, Open: false, OrderPriority: -17);
        Register(new("Variation Seed", "Image-variation seed.\nCombined partially with the original seed to create a similar-but-different image for the same seed.\n-1 = random.",
            T2IParamDataType.INTEGER, "-1", (s, p) => p.VarSeed = int.Parse(s), Min: -1, Max: uint.MaxValue, Step: 1, Examples: new[] { "1", "2", "...", "10" }, OrderPriority: -17, NumberView: NumberViewType.BIG, Group: variationGroup
            ));
        Register(new("Variation Seed Strength", "How strongly to apply the variation seed.\n0 = don't use, 1 = replace the base seed entirely. 0.5 is a good value.",
            T2IParamDataType.DECIMAL, "0", (s, p) => p.VarSeedStrength = float.Parse(s), Min: 0, Max: 1, Step: 0.05, Examples: new[] { "0", "0.25", "0.5", "0.75" }, OrderPriority: -17, NumberView: NumberViewType.SLIDER, Group: variationGroup
            ));
        T2IParamGroup resolutionGroup = new("Resolution", Toggles: false, Open: false, OrderPriority: -11);
        Register(new("Aspect Ratio", "Image aspect ratio. Some models can stretch better than others.",
            T2IParamDataType.DROPDOWN, "1:1", (s, p) => p.OtherParams["aspectratio"] = s, GetValues: (_) => new() { "1:1", "4:3", "3:2", "8:5", "16:9", "21:9", "3:4", "2:3", "5:8", "9:16", "9:21", "Custom" }, OrderPriority: -11, Group: resolutionGroup
            ));
        Register(new("Width", "Image width, in pixels.\nSDv1 uses 512, SDv2 uses 768, SDXL prefers 1024.\nSome models allow variation within a range (eg 512 to 768) but almost always want a multiple of 64.",
            T2IParamDataType.INTEGER, "512", (s, p) => p.Width = int.Parse(s), Min: 128, Max: 4096, Step: 64, Examples: new[] { "512", "768", "1024" }, OrderPriority: -10, NumberView: NumberViewType.POT_SLIDER, Group: resolutionGroup
            ));
        Register(new("Height", "Image height, in pixels.\nSDv1 uses 512, SDv2 uses 768, SDXL prefers 1024.\nSome models allow variation within a range (eg 512 to 768) but almost always want a multiple of 64.",
            T2IParamDataType.INTEGER, "512", (s, p) => p.Height = int.Parse(s), Min: 128, Max: 4096, Step: 64, Examples: new[] { "512", "768", "1024" }, OrderPriority: -9, NumberView: NumberViewType.POT_SLIDER, Group: resolutionGroup
            ));
        T2IParamGroup initImageGroup = new("Init Image", Toggles: true, Open: false, OrderPriority: -5);
        Register(new("Init Image", "Init-image, to edit an image using diffusion.\nThis process is sometimes called 'img2img' or 'Image To Image'.",
            T2IParamDataType.IMAGE, "", (s, p) => p.InitImage = string.IsNullOrWhiteSpace(s) ? null : new(s), OrderPriority: -5, Group: initImageGroup
            ));
        Register(new("Init Image Creativity", "Higher values make the generation more creative, lower values follow the init image closer.\nSometimes referred to as 'Denoising Strength' for 'img2img'.",
            T2IParamDataType.DECIMAL, "0.6", (s, p) => p.ImageInitStrength = float.Parse(s), Min: 0, Max: 1, Step: 0.05, OrderPriority: -4.5, NumberView: NumberViewType.SLIDER, Group: initImageGroup
            ));
        Register(new("Model", "What main checkpoint model should be used.",
            T2IParamDataType.DROPDOWN, "", (s, p) => p.Model = Program.T2IModels.Models[s], GetValues: (session) => Program.T2IModels.ListModelsFor(session).Select(m => m.Name).Order().ToList(), Permission: "param_model", VisibleNormally: false
            ));
        Register(new("[Internal] Backend Type", "Which StableUI backend type should be used for this request.",
            T2IParamDataType.DROPDOWN, "Any", (s, p) => p.BackendType = s, GetValues: (_) => Program.Backends.BackendTypes.Keys.ToList(), IsAdvanced: true, Permission: "param_backend_type", Toggleable: true
            ));
    }

    /// <summary>Gets the value in the list that best matches the input text (for user input handling).</summary>
    public static string GetBestInList(string name, List<string> list)
    {
        string backup = null;
        int bestLen = 999;
        name = CleanNameGeneric(name);
        foreach (string listVal in list)
        {
            string listValClean = CleanNameGeneric(listVal);
            if (listValClean == name)
            {
                return listVal;
            }
            if (listValClean.Contains(name))
            {
                if (listValClean.Length < bestLen)
                {
                    backup = listVal;
                    bestLen = listValClean.Length;
                }
            }
        }
        return backup;
    }

    /// <summary>Quick hex validator.</summary>
    public static AsciiMatcher ValidBase64Matcher = new(AsciiMatcher.BothCaseLetters + AsciiMatcher.Digits + "+/=");

    /// <summary>Converts a parameter value in a valid input for that parameter, or throws <see cref="InvalidDataException"/> if it can't.</summary>
    public static string ValidateParam(T2IParamType type, string val, Session session)
    {
        if (type is null)
        {
            throw new InvalidDataException("Unknown parameter type");
        }
        if (type.Clean is not null)
        {
            val = type.Clean(val);
        }
        switch (type.Type)
        {
            case T2IParamDataType.INTEGER:
                if (!int.TryParse(val, out int valInt))
                {
                    throw new InvalidDataException($"Invalid integer value for param {type.Name} - must be a valid integer (eg '0', '3', '-5', etc)");
                }
                if (type.Min != 0 || type.Max != 0)
                {
                    if (valInt < type.Min || valInt > type.Max)
                    {
                        throw new InvalidDataException($"Invalid integer value for param {type.Name} - must be between {type.Min} and {type.Max}");
                    }
                }
                return valInt.ToString();
            case T2IParamDataType.DECIMAL:
                if (!double.TryParse(val, out double valDouble))
                {
                    throw new InvalidDataException($"Invalid decimal value for param {type.Name} - must be a valid decimal (eg '0.0', '3.5', '-5.2', etc)");
                }
                if (type.Min != 0 || type.Max != 0)
                {
                    if (valDouble < type.Min || valDouble > type.Max)
                    {
                        throw new InvalidDataException($"Invalid decimal value for param {type.Name} - must be between {type.Min} and {type.Max}");
                    }
                }
                return valDouble.ToString();
            case T2IParamDataType.BOOLEAN:
                val = val.ToLowerFast();
                if (val != "true" && val != "false")
                {
                    throw new InvalidDataException($"Invalid boolean value for param {type.Name} - must be exactly 'true' or 'false'");
                }
                return val;
            case T2IParamDataType.TEXT:
            case T2IParamDataType.DROPDOWN:
                if (type.GetValues is not null && type.ValidateValues)
                {
                    val = GetBestInList(val, type.GetValues(session));
                    if (val is null)
                    {
                        throw new InvalidDataException($"Invalid value for param {type.Name} - must be one of: `{string.Join("`, `", type.GetValues(session))}`");
                    }
                }
                return val;
            case T2IParamDataType.IMAGE:
                if (val.StartsWith("data:"))
                {
                    val = val.After(',');
                }
                if (string.IsNullOrWhiteSpace(val))
                {
                    return "";
                }
                if (!ValidBase64Matcher.IsOnlyMatches(val) || val.Length < 10)
                {
                    throw new InvalidDataException($"Invalid image value for param {type.Name} - must be a valid base64 string");
                }
                return val;
        }
        throw new InvalidDataException($"Unknown parameter type's data type? {type.Type}");
    }

    /// <summary>Takes user input of a parameter and applies it to the parameter tracking data object.</summary>
    public static void ApplyParameter(string paramTypeName, string value, T2IParams data)
    {
        if (!Types.TryGetValue(CleanTypeName(paramTypeName), out T2IParamType type))
        {
            throw new InvalidDataException("Unrecognized parameter type name.");
        }
        if (type.Permission is not null)
        {
            if (!data.SourceSession.User.HasGenericPermission(type.Permission))
            {
                throw new InvalidDataException($"You do not have permission to use parameter {type.Name}.");
            }
        }
        value = ValidateParam(type, value, data.SourceSession);
        type.Apply(value, data);
        if (type.FeatureFlag is not null)
        {
            data.RequiredFlags.Add(type.FeatureFlag);
        }
    }
}
