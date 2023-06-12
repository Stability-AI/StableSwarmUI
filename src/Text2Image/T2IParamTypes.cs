using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableUI.Accounts;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Utils;

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
    /// <summary>Special-case integer input: Power-of-Two slider, used especially for Width/Height of an image.</summary>
    POT_SLIDER,
    /// <summary>Input is just 'true' or 'false'; a checkbox.</summary>
    BOOLEAN,
    /// <summary>Selection explicitly from a list.</summary>
    DROPDOWN
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
public record class T2IParamType(string Name, string Description, T2IParamDataType Type, string Default, Action<string, T2IParams> Apply, double Min = 0, double Max = 0, double Step = 1,
    Func<string, string> Clean = null, Func<Session, List<string>> GetValues = null, string[] Examples = null, Func<List<string>, List<string>> ParseList = null, bool ValidateValues = true,
    bool VisibleNormally = true, bool IsAdvanced = false, string FeatureFlag = null, string Permission = null, bool Toggleable = false, double OrderPriority = 10)
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
            ["priority"] = OrderPriority
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
        Register(new("Prompt", "The input prompt text that describes the image you want to generate.",
            T2IParamDataType.TEXT, "", (s, p) => p.Prompt = ApplyStringEdit(p.Prompt, s), Examples: new[] { "a photo of a cat", "a cartoonish drawing of an astronaut" }, OrderPriority: -100
            ));
        Register(new("Negative Prompt", "Like the input prompt text, but describe what NOT to generate.",
            T2IParamDataType.TEXT, "", (s, p) => p.NegativePrompt = ApplyStringEdit(p.NegativePrompt, s), Examples: new[] { "ugly, bad, gross", "lowres, low quality" }, OrderPriority: -90
            ));
        Register(new("Images", "How many images to generate at once.",
            T2IParamDataType.INTEGER, "1", (s, p) => { }, Min: 1, Max: 100, Step: 1, Examples: new[] { "1", "4" }, OrderPriority: -50
            ));
        Register(new("Steps", "How many times to run the model. More steps = better quality, but more time.",
            T2IParamDataType.INTEGER, "20", (s, p) => p.Steps = int.Parse(s), Min: 1, Max: 200, Step: 1, Examples: new[] { "10", "15", "20", "25", "30" }, OrderPriority: -20
            ));
        Register(new("Seed", "Image seed. -1 = random.",
            T2IParamDataType.INTEGER, "-1", (s, p) => p.Seed = int.Parse(s), Min: -1, Max: int.MaxValue, Step: 1, Examples: new[] { "1", "2", "...", "10" }, OrderPriority: -19
            ));
        Register(new("CFG Scale", "How strongly to scale prompt input. Too-high values can cause corrupted/burnt images, too-low can cause nonsensical images.",
            T2IParamDataType.DECIMAL, "7", (s, p) => p.CFGScale = float.Parse(s), Min: 0, Max: 30, Step: 0.25, Examples: new[] { "5", "6", "7", "8", "9" }, OrderPriority: -18
            ));
        Register(new("Width", "Image width, in pixels.",
            T2IParamDataType.POT_SLIDER, "1024", (s, p) => p.Width = int.Parse(s), Min: 128, Max: 4096, Step: 64, Examples: new[] { "512", "768", "1024" }, OrderPriority: -10
            ));
        Register(new("Height", "Image height, in pixels.",
            T2IParamDataType.POT_SLIDER, "1024", (s, p) => p.Height = int.Parse(s), Min: 128, Max: 4096, Step: 64, Examples: new[] { "512", "768", "1024" }, OrderPriority: -9
            ));
        Register(new("Model", "What model should be used.",
            T2IParamDataType.DROPDOWN, "", (s, p) => p.Model = Program.T2IModels.Models[s], GetValues: (session) => Program.T2IModels.ListModelsFor(session).Select(m => m.Name).ToList(), Permission: "param_model", VisibleNormally: false
            ));
        Register(new("[Internal] Backend Type", "Which backend type should be used for this request.",
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
            case T2IParamDataType.POT_SLIDER:
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
