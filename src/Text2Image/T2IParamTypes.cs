using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;
using System.IO;

namespace StableSwarmUI.Text2Image;

/// <summary>Represents the data-type of a Text2Image parameter type.</summary>
public enum T2IParamDataType
{
    /// <summary>Default/unset value to be filled.</summary>
    UNSET,
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
    IMAGE,
    /// <summary>Model reference input.</summary>
    MODEL,
    /// <summary>Multi-select or comma-separated data list.</summary>
    LIST,
    /// <summary>List of images.</summary>
    IMAGE_LIST
}

/// <summary>Which format to display a number in.</summary>
public enum ParamViewType
{
    /// <summary>Use whatever the default is.</summary>
    NORMAL,
    /// <summary>Prompt-text box.</summary>
    PROMPT,
    /// <summary>Small numeric input box.</summary>
    SMALL,
    /// <summary>Large input box.</summary>
    BIG,
    /// <summary>Ordinary range slider.</summary>
    SLIDER,
    /// <summary>Power-of-Two slider, used especially for Width/Height of an image.</summary>
    POT_SLIDER,
    /// <summary>Random-seed input.</summary>
    SEED
}

/// <summary>
/// Defines a parameter type for Text2Image, in full, for usage in the UI and more.
/// </summary>
/// <param name="Name">The (full, proper) name of the parameter.</param>
/// <param name="Description">A user-friendly description text of what the parameter is/does.</param>
/// <param name="Default">A default value for this parameter.</param>
/// <param name="Min">(For numeric types) the minimum value.</param>
/// <param name="Max">(For numeric types) the maximum value.</param>
/// <param name="ViewMax">(For numeric types) the *visual* minimum value (allowed to exceed).</param>
/// <param name="ViewMax">(For numeric types) the *visual* maximum value (allowed to exceed).</param>
/// <param name="Step">(For numeric types) the step rate for UI usage.</param>
/// <param name="Clean">An optional special method to clean up text input (input = prior,new). Prior can be null.</param>
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
/// <param name="IgnoreIf">Ignore this parameter if the value is equal to this.</param>
/// <param name="ViewType">How to display a number input.</param>
/// <param name="HideFromMetadata">Whether to hide this parameter from image metadata.</param>
/// <param name="MetadataFormat">Optional function to reformat value for display in metadata.</param>
/// <param name="AlwaysRetain">If true, the parameter will be retained when otherwise it would be removed (for example, by comfy workflow usage).</param>
/// <param name="ChangeWeight">Weighting value used to indicate, as a relative weight, how much processing time is needed to change the value of this parameter type - this is used for example for grids to do speed priority sorting. 0 is normal, 10 is model change.</param>
/// <param name="ExtraHidden">If true, agressively hide from anything.</param>
/// <param name="Type">The type of the type - text vs integer vs etc (will be set when registering).</param>
/// <param name="DoNotSave">Can be set to forbid tracking/saving of a param value.</param>
/// <param name="ImageShouldResize">(For Image-type params) If true, the image should resize to match the target resolution.</param>
/// <param name="ImageAlwaysB64">(For Image-type params) If true, always use B64 (never file).</param>
/// <param name="DoNotPreview">If this is true, the parameter is unfit for previewing (eg long generation addons or unnecessary refinements).</param>
/// <param name="Subtype">The sub-type of the type - for models, this might be eg "Stable-Diffusion".</param>
/// <param name="ID">The raw ID of this parameter (will be set when registering).</param>
/// <param name="SharpType">The C# datatype.</param>
/// 
public record class T2IParamType(string Name, string Description, string Default, double Min = 0, double Max = 0, double Step = 1, double ViewMin = 0, double ViewMax = 0,
    Func<string, string, string> Clean = null, Func<Session, List<string>> GetValues = null, string[] Examples = null, Func<List<string>, List<string>> ParseList = null, bool ValidateValues = true,
    bool VisibleNormally = true, bool IsAdvanced = false, string FeatureFlag = null, string Permission = null, bool Toggleable = false, double OrderPriority = 10, T2IParamGroup Group = null, string IgnoreIf = null,
    ParamViewType ViewType = ParamViewType.SMALL, bool HideFromMetadata = false, Func<string, string> MetadataFormat = null, bool AlwaysRetain = false, double ChangeWeight = 0, bool ExtraHidden = false,
    T2IParamDataType Type = T2IParamDataType.UNSET, bool DoNotSave = false, bool ImageShouldResize = true, bool ImageAlwaysB64 = false, bool DoNotPreview = false, string Subtype = null, string ID = null, Type SharpType = null)
{
    public JObject ToNet(Session session)
    {
        return new JObject()
        {
            ["name"] = Name,
            ["id"] = ID,
            ["description"] = Description,
            ["type"] = Type.ToString().ToLowerFast(),
            ["subtype"] = Subtype,
            ["default"] = Default,
            ["min"] = Min,
            ["max"] = Max,
            ["view_min"] = ViewMin,
            ["view_max"] = ViewMax,
            ["step"] = Step,
            ["values"] = GetValues == null ? null : JToken.FromObject(GetValues(session)),
            ["examples"] = Examples == null ? null : JToken.FromObject(Examples),
            ["visible"] = VisibleNormally,
            ["advanced"] = IsAdvanced,
            ["feature_flag"] = FeatureFlag,
            ["toggleable"] = Toggleable,
            ["priority"] = OrderPriority,
            ["group"] = Group?.ToNet(session),
            ["always_retain"] = AlwaysRetain,
            ["do_not_save"] = DoNotSave,
            ["do_not_preview"] = DoNotPreview,
            ["view_type"] = ViewType.ToString().ToLowerFast(),
            ["extra_hidden"] = ExtraHidden
        };
    }

    public static T2IParamType FromNet(JObject data)
    {
        string getStr(string key) => data.TryGetValue(key, out JToken tok) && tok.Type != JTokenType.Null ? $"{tok}" : null;
        double getDouble(string key) => data.TryGetValue(key, out JToken tok) && tok.Type != JTokenType.Null && double.TryParse($"{tok}", out double tokVal) ? tokVal : 0;
        bool getBool(string key, bool def) => data.TryGetValue(key, out JToken tok) && tok.Type != JTokenType.Null && bool.TryParse($"{tok}", out bool tokVal) ? tokVal : def;
        T getEnum<T>(string key, T def) where T : struct => data.TryGetValue(key, out JToken tok) && tok.Type != JTokenType.Null && Enum.TryParse($"{tok}", true, out T tokVal) ? tokVal : def;
        List<string> getList(string key) => data.TryGetValue(key, out JToken tok) && tok.Type != JTokenType.Null ? tok.ToObject<List<string>>() : null;
        List<string> vals = getList("values");
        List<string> examples = getList("examples");
        T2IParamDataType type = getEnum("type", T2IParamDataType.UNSET);
        return new(Name: getStr("name"), Description: getStr("description"), Default: getStr("default"), ID: getStr("id"),
            Type: type, SharpType: T2IParamTypes.DataTypeToSharpType(type),
            Min: getDouble("min"), Max: getDouble("max"), Step: getDouble("step"), ViewMax: getDouble("view_max"), OrderPriority: getDouble("priority"),
            GetValues: vals is null ? null : _ => vals, Examples: examples?.ToArray(), Subtype: getStr("subtype"), FeatureFlag: getStr("feature_flag"),
            VisibleNormally: getBool("visible", true), IsAdvanced: getBool("advanced", false), AlwaysRetain: getBool("always_retain", false),
            ImageShouldResize: getBool("image_should_resize", true), ImageAlwaysB64: getBool("image_always_b64", false),
            DoNotSave: getBool("do_not_save", false), DoNotPreview: getBool("do_not_preview", false), ViewType: getEnum("view_type", ParamViewType.SMALL));
    }
}

/// <summary>Helper class to easily read T2I Parameters.</summary>
/// <typeparam name="T">The C# datatype of the parameter.</typeparam>
public class T2IRegisteredParam<T>
{
    /// <summary>The underlying type data.</summary>
    public T2IParamType Type;
}

/// <summary>Represents a group of parameters.</summary>
/// <param name="Name">The name of the group.</param>
/// <param name="Toggles">If true, the entire group toggles as one.</param>
/// <param name="Open">If true, the group defaults open. If false, it defaults to closed.</param>
/// <param name="OrderPriority">The priority order position to put this group in.</param>
/// <param name="Description">Optional description/explanation text of the group.</param>
/// <param name="IsAdvanced">If true, this is an advanced setting group that should be hidden by a dropdown.</param>
/// <param name="CanShrink">If true, the group can be shrunk on-page to hide it. If false, it is always open.</param>
public record class T2IParamGroup(string Name, bool Toggles = false, bool Open = true, double OrderPriority = 10, string Description = "", bool IsAdvanced = false, bool CanShrink = true)
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
            ["description"] = Description,
            ["advanced"] = IsAdvanced,
            ["can_shrink"] = CanShrink
        };
    }
}

/// <summary>Central manager of Text2Image parameter types.</summary>
public class T2IParamTypes
{
    /// <summary>Map of all currently loaded types, by cleaned name.</summary>
    public static Dictionary<string, T2IParamType> Types = [];

    /// <summary>Helper to match valid text for use in a parameter type name.</summary>
    public static AsciiMatcher CleanTypeNameMatcher = new(AsciiMatcher.LowercaseLetters);

    public static T2IParamDataType SharpTypeToDataType(Type t, bool hasValues)
    {
        if (t == typeof(int) || t == typeof(long)) return T2IParamDataType.INTEGER;
        if (t == typeof(float) || t == typeof(double)) return T2IParamDataType.DECIMAL;
        if (t == typeof(bool)) return T2IParamDataType.BOOLEAN;
        if (t == typeof(string)) return hasValues ? T2IParamDataType.DROPDOWN : T2IParamDataType.TEXT;
        if (t == typeof(Image)) return T2IParamDataType.IMAGE;
        if (t == typeof(T2IModel)) return T2IParamDataType.MODEL;
        if (t == typeof(List<string>)) return T2IParamDataType.LIST;
        if (t == typeof(List<Image>)) return T2IParamDataType.IMAGE_LIST;
        return T2IParamDataType.UNSET;
    }

    public static Type DataTypeToSharpType(T2IParamDataType t)
    {
        return t switch {
            T2IParamDataType.INTEGER => typeof(long),
            T2IParamDataType.DECIMAL => typeof(double),
            T2IParamDataType.BOOLEAN => typeof(bool),
            T2IParamDataType.TEXT => typeof(string),
            T2IParamDataType.DROPDOWN => typeof(string),
            T2IParamDataType.IMAGE => typeof(Image),
            T2IParamDataType.MODEL => typeof(T2IModel),
            T2IParamDataType.LIST => typeof(List<string>),
            T2IParamDataType.IMAGE_LIST => typeof(List<Image>),
            _ => null
        };
    }

    /// <summary>Register a new parameter type.</summary>
    public static T2IRegisteredParam<T> Register<T>(T2IParamType type)
    {
        type = type with { ID = CleanTypeName(type.Name), Type = SharpTypeToDataType(typeof(T), type.GetValues != null), SharpType = typeof(T) };
        Types.Add(type.ID, type);
        LanguagesHelper.AppendSetInternal(type.Name, type.Description);
        return new T2IRegisteredParam<T>() { Type = type };
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

    /// <summary>Strips ".safetensors" from the end of model name for cleanliness.</summary>
    public static string CleanModelName(string name)
    {
        if (name.EndsWithFast(".safetensors"))
        {
            name = name.BeforeLast(".safetensors");
        }
        return name;
    }

    /// <summary>Strips ".safetensors" from the end of model name comma-separated-lists for cleanliness.</summary>
    public static string CleanModelNameList(string names)
    {
        return names.SplitFast(',').Select(s => CleanModelName(s.Trim())).JoinString(",");
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

    public static T2IRegisteredParam<string> Prompt, NegativePrompt, AspectRatio, BackendType, RefinerMethod, FreeUApplyTo, PersonalNote, VideoFormat, VideoResolution, UnsamplerPrompt, ImageFormat, MaskBehavior, RawResolution, SeamlessTileable, SD3TextEncs;
    public static T2IRegisteredParam<int> Images, Steps, Width, Height, BatchSize, ExactBackendID, VAETileSize, ClipStopAtLayer, VideoFrames, VideoMotionBucket, VideoFPS, VideoSteps, RefinerSteps, CascadeLatentCompression, MaskShrinkGrow, MaskBlur, SegmentMaskBlur, SegmentMaskGrow;
    public static T2IRegisteredParam<long> Seed, VariationSeed, WildcardSeed;
    public static T2IRegisteredParam<double> CFGScale, VariationSeedStrength, InitImageCreativity, InitImageResetToNorm, RefinerControl, RefinerUpscale, ReVisionStrength, AltResolutionHeightMult,
        FreeUBlock1, FreeUBlock2, FreeUSkip1, FreeUSkip2, GlobalRegionFactor, EndStepsEarly, SamplerSigmaMin, SamplerSigmaMax, SamplerRho, VideoAugmentationLevel, VideoCFG, VideoMinCFG, IP2PCFG2, RegionalObjectCleanupFactor, SigmaShift, SegmentThresholdMax;
    public static T2IRegisteredParam<Image> InitImage, MaskImage;
    public static T2IRegisteredParam<T2IModel> Model, RefinerModel, VAE, ReVisionModel, RegionalObjectInpaintingModel, SegmentModel, VideoModel, RefinerVAE;
    public static T2IRegisteredParam<List<string>> Loras, LoraWeights, LoraSectionConfinement;
    public static T2IRegisteredParam<List<Image>> PromptImages;
    public static T2IRegisteredParam<bool> SaveIntermediateImages, DoNotSave, ControlNetPreviewOnly, RevisionZeroPrompt, RemoveBackground, NoSeedIncrement, NoPreviews, VideoBoomerang, ModelSpecificEnhancements, UseInpaintingEncode, SaveSegmentMask, InitImageRecompositeMask, UseReferenceOnly, RefinerDoTiling;

    public static T2IParamGroup GroupRevision, GroupCore, GroupVariation, GroupResolution, GroupSampling, GroupInitImage, GroupRefiners,
        GroupAdvancedModelAddons, GroupSwarmInternal, GroupFreeU, GroupRegionalPrompting, GroupAdvancedSampling, GroupVideo;

    public class ControlNetParamHolder
    {
        public T2IParamGroup Group;

        public T2IRegisteredParam<Image> Image;

        public T2IRegisteredParam<double> Strength, Start, End;

        public T2IRegisteredParam<T2IModel> Model;

        public string NameSuffix = "";
    }

    public static ControlNetParamHolder[] Controlnets = new ControlNetParamHolder[3];

    /// <summary>(For extensions) list of functions that provide fake types for given type names.</summary>
    public static List<Func<string, T2IParamInput, T2IParamType>> FakeTypeProviders = [];

    /// <summary>(Called by <see cref="Program"/> during startup) registers all default parameter types.</summary>
    public static void RegisterDefaults()
    {
        Prompt = Register<string>(new("Prompt", "The input prompt text that describes the image you want to generate.\nTell the AI what you want to see.",
            "", Clean: ApplyStringEdit, Examples: ["a photo of a cat", "a cartoonish drawing of an astronaut"], OrderPriority: -100, VisibleNormally: false, ViewType: ParamViewType.PROMPT, ChangeWeight: -5
            ));
        PromptImages = Register<List<Image>>(new("Prompt Images", "Images to include with the prompt, for eg ReVision or UnCLIP.\nIf this parameter is visible, you've done something wrong - this parameter is tracked internally.",
            "", IgnoreIf: "", OrderPriority: -95, Toggleable: true, VisibleNormally: false, IsAdvanced: true, ImageShouldResize: false, ChangeWeight: 2, HideFromMetadata: true // Has special internal handling
            ));
        GroupAdvancedModelAddons = new("Advanced Model Addons", Open: false, IsAdvanced: true);
        NegativePrompt = Register<string>(new("Negative Prompt", "Like the input prompt text, but describe what NOT to generate.\nTell the AI things you don't want to see.",
            "", IgnoreIf: "", Clean: ApplyStringEdit, Examples: ["ugly, bad, gross", "lowres, low quality"], OrderPriority: -90, ViewType: ParamViewType.PROMPT, ChangeWeight: -5, VisibleNormally: false
            ));
        GroupRevision = new("ReVision", Open: false, Toggles: true, OrderPriority: -70, Description: $"Image prompting with ReVision, IP-Adapter, etc.\n<a href=\"{Utilities.RepoDocsRoot}/Features/IPAdapter-ReVision.md\">See more docs here.</a>");
        ReVisionStrength = Register<double>(new("ReVision Strength", $"How strong to apply ReVision image inputs.\nSet to 0 to disable ReVision processing.",
            "1", OrderPriority: -70, Min: 0, Max: 10, Step: 0.1, ViewType: ParamViewType.SLIDER, Group: GroupRevision
            ));
        RevisionZeroPrompt = Register<bool>(new("ReVision Zero Prompt", "Zeroes the prompt and negative prompt for ReVision inputs.\nApplies only to the base, the refiner will still get prompts.\nIf you want zeros on both, just delete your prompt text.\nIf not checked, empty prompts will be zeroed regardless.",
            "false", IgnoreIf: "false", Group: GroupRevision
            ));
        UseReferenceOnly = Register<bool>(new("Use Reference Only", "Use the 'Reference-Only' technique to guide the generation towards the input image.\nThis currently has side effects that notably prevent Batch from being used properly.",
            "false", IgnoreIf: "false", Group: GroupRevision
            ));
        ReVisionModel = Register<T2IModel>(new("ReVision Model", "The CLIP Vision model to use for ReVision inputs.\nThis will also override IPAdapter (if IPAdapter-G is in use).",
            "", Subtype: "ClipVision", IsAdvanced: true, Toggleable: true, Group: GroupAdvancedModelAddons
            ));
        GroupCore = new("Core Parameters", Toggles: false, Open: true, OrderPriority: -50);
        Images = Register<int>(new("Images", "How many images to generate at once.",
            "1", IgnoreIf: "1", Min: 1, Max: 10000, Step: 1, Examples: ["1", "4"], OrderPriority: -50, Group: GroupCore
            ));
        Seed = Register<long>(new("Seed", "Image seed.\n-1 = random.\nDifferent seeds produce different results for the same prompt.",
            "-1", Min: -1, Max: long.MaxValue, Step: 1, Examples: ["1", "2", "...", "10"], OrderPriority: -30, ViewType: ParamViewType.SEED, Group: GroupCore, ChangeWeight: -5
            ));
        Steps = Register<int>(new("Steps", "How many times to run the model.\nMore steps = better quality, but more time.\n20 is a good baseline for speed, 40 is good for maximizing quality.\nYou can go much higher, but it quickly becomes pointless above 70 or so.",
            "20", Min: 0, Max: 200, ViewMax: 100, Step: 1, Examples: ["10", "15", "20", "30", "40"], OrderPriority: -20, Group: GroupCore, ViewType: ParamViewType.SLIDER
            ));
        CFGScale = Register<double>(new("CFG Scale", "How strongly to scale prompt input.\nHigher CFG scales tend to produce more contrast, and lower CFG scales produce less contrast.\n"
            + "Too-high values can cause corrupted/burnt images, too-low can cause nonsensical images.\n7 is a good baseline. Normal usages vary between 5 and 9.",
            "7", Min: 1, Max: 100, ViewMax: 20, Step: 0.5, Examples: ["5", "6", "7", "8", "9"], OrderPriority: -18, ViewType: ParamViewType.SLIDER, Group: GroupCore, ChangeWeight: -3
            ));
        GroupVariation = new("Variation Seed", Toggles: true, Open: false, OrderPriority: -17);
        VariationSeed = Register<long>(new("Variation Seed", "Image-variation seed.\nCombined partially with the original seed to create a similar-but-different image for the same seed.\n-1 = random.",
            "-1", Min: -1, Max: uint.MaxValue, Step: 1, Examples: ["1", "2", "...", "10"], OrderPriority: -17, ViewType: ParamViewType.SEED, Group: GroupVariation, FeatureFlag: "variation_seed", ChangeWeight: -4
            ));
        VariationSeedStrength = Register<double>(new("Variation Seed Strength", "How strongly to apply the variation seed.\n0 = don't use, 1 = replace the base seed entirely. 0.5 is a good value.",
            "0", IgnoreIf: "0", Min: 0, Max: 1, Step: 0.05, Examples: ["0", "0.25", "0.5", "0.75"], OrderPriority: -17, ViewType: ParamViewType.SLIDER, Group: GroupVariation, FeatureFlag: "variation_seed", ChangeWeight: -4
            ));
        GroupResolution = new("Resolution", Toggles: false, Open: false, OrderPriority: -11);
        AspectRatio = Register<string>(new("Aspect Ratio", "Image aspect ratio. Some models can stretch better than others.",
            "1:1", GetValues: (_) => ["1:1", "4:3", "3:2", "8:5", "16:9", "21:9", "3:4", "2:3", "5:8", "9:16", "9:21", "Custom"], OrderPriority: -11, Group: GroupResolution
            ));
        Width = Register<int>(new("Width", "Image width, in pixels.\nSDv1 uses 512, SDv2 uses 768, SDXL prefers 1024.\nSome models allow variation within a range (eg 512 to 768) but almost always want a multiple of 64.",
            "512", Min: 64, ViewMin: 256, Max: 16384, ViewMax: 2048, Step: 32, Examples: ["512", "768", "1024"], OrderPriority: -10, ViewType: ParamViewType.POT_SLIDER, Group: GroupResolution
            ));
        Height = Register<int>(new("Height", "Image height, in pixels.\nSDv1 uses 512, SDv2 uses 768, SDXL prefers 1024.\nSome models allow variation within a range (eg 512 to 768) but almost always want a multiple of 64.",
            "512", Min: 64, ViewMin: 256, Max: 16384, ViewMax: 2048, Step: 32, Examples: ["512", "768", "1024"], OrderPriority: -9, ViewType: ParamViewType.POT_SLIDER, Group: GroupResolution
            ));
        GroupSampling = new("Sampling", Toggles: false, Open: false, OrderPriority: -8);
        SD3TextEncs = Register<string>(new("SD3 TextEncs", "Which text encoders to use for Stable Diffusion 3 (SD3) models.\nCan use CLIP pairs, or T5, or both.\nBoth is the standard way to run SD3, but CLIP only uses fewer system resources.",
            "CLIP Only", GetValues: _ => ["CLIP Only", "T5 Only", "CLIP + T5"], Toggleable: true, Group: GroupSampling, FeatureFlag: "sd3", OrderPriority: 5
            ));
        SeamlessTileable = Register<string>(new("Seamless Tileable", "Makes the generated image seamlessly tileable (like a 3D texture would be).\nOptionally, can be tileable on only the X axis (horizontal) or Y axis (vertical).",
            "false", IgnoreIf: "false", GetValues: _ => ["false", "true", "X-Only", "Y-Only"], Group: GroupSampling, FeatureFlag: "seamless", OrderPriority: 15
            ));
        GroupInitImage = new("Init Image", Toggles: true, Open: false, OrderPriority: -5);
        InitImage = Register<Image>(new("Init Image", "Init-image, to edit an image using diffusion.\nThis process is sometimes called 'img2img' or 'Image To Image'.",
            null, OrderPriority: -5, Group: GroupInitImage, ChangeWeight: 2
            ));
        InitImageCreativity = Register<double>(new("Init Image Creativity", "Higher values make the generation more creative, lower values follow the init image closer.\nSometimes referred to as 'Denoising Strength' for 'img2img'.",
            "0.6", Min: 0, Max: 1, Step: 0.05, OrderPriority: -4.5, ViewType: ParamViewType.SLIDER, Group: GroupInitImage, Examples: ["0", "0.4", "0.6", "1"]
            ));
        InitImageResetToNorm = Register<double>(new("Init Image Reset To Norm", "Merges the init image towards the latent norm.\nThis essentially lets you boost 'init image creativity' past 1.0.\nSet to 0 to disable.",
            "0", IgnoreIf: "0", Min: 0, Max: 1, Step: 0.05, OrderPriority: -4.5, ViewType: ParamViewType.SLIDER, Group: GroupInitImage, Examples: ["0", "0.2", "0.5", "1"]
            ));
        MaskImage = Register<Image>(new("Mask Image", "Mask-image, white pixels are changed, black pixels are not changed, gray pixels are half-changed.",
            null, OrderPriority: -4, Group: GroupInitImage, ChangeWeight: 2
            ));
        MaskShrinkGrow = Register<int>(new("Mask Shrink Grow", "If enabled, the image will be shrunk to just the mask, and then grow by this value many pixels.\nAfter that, the generation process will run in full, and the image will be composited back into the original image at the end.\nThis allows for refining small details of an image for effectively.\nThis is also known as 'Inpaint Only Masked'.\nLarger values increase the surrounding context the generation receives, lower values contain it tighter and allow the AI to create more detail.",
            "8", Toggleable: true, Min: 0, Max: 512, OrderPriority: -3.7, Group: GroupInitImage, Examples: ["0", "8", "32"]
            ));
        MaskBlur = Register<int>(new("Mask Blur", "If enabled, the mask will be blurred by this blur factor.\nThis makes the transition for the new image smoother.\nSet to 0 to disable.",
            "4", IgnoreIf: "0", Min: 0, Max: 64, OrderPriority: -3.6, Group: GroupInitImage, Examples: ["0", "4", "8", "16"]
            ));
        MaskBehavior = Register<string>(new("Mask Behavior", "How to process the mask.\n'Differential' = 'Differential Diffusion' technique, wherein the mask values are used as offsets for timestep of when to apply the mask or not.\n'Simple Latent' = the most basic latent masking technique.",
            "Differential", Toggleable: true, IsAdvanced: true, GetValues: (_) => ["Differential", "Simple Latent"], OrderPriority: -3.5, Group: GroupInitImage
            ));
        InitImageRecompositeMask = Register<bool>(new("Init Image Recomposite Mask", "If enabled and a mask is in use, this will recomposite the masked generated onto the original image for a cleaner result.\nIf disabled, VAE artifacts may build up across repeated inpaint operations.\nDefaults enabled.",
            "true", IgnoreIf: "true", Group: GroupInitImage, OrderPriority: -3.4, IsAdvanced: true
            ));
        UseInpaintingEncode = Register<bool>(new("Use Inpainting Encode", "Uses VAE Encode logic specifically designed for certain inpainting models.\nNotably this includes the RunwayML Stable-Diffusion-v1 Inpainting model.\nThis covers the masked area with gray.",
            "false", IgnoreIf: "false", Group: GroupInitImage, OrderPriority: -3.2, IsAdvanced: true
            ));
        UnsamplerPrompt = Register<string>(new("Unsampler Prompt", "If enabled, feeds this prompt to an unsampler before resampling with your main prompt.\nThis is powerful for controlled image editing.\n\nFor example, use unsampler prompt 'a photo of a man wearing a black hat',\nand give main prompt 'a photo of a man wearing a sombrero', to change what type of hat a person is wearing.",
            "", OrderPriority: -3, Toggleable: true, ViewType: ParamViewType.PROMPT, Group: GroupInitImage
            ));
        GroupRefiners = new("Refiner", Toggles: true, Open: false, OrderPriority: -3);
        static List<string> listRefinerModels(Session s)
        {
            List<T2IModel> baseList = [.. Program.MainSDModels.ListModelsFor(s).OrderBy(m => m.Name)];
            List<T2IModel> refinerList = baseList.Where(m => m.ModelClass is not null && m.ModelClass.Name.Contains("Refiner")).ToList();
            List<string> bases = baseList.Select(m => CleanModelName(m.Name)).ToList();
            return ["(Use Base)", .. refinerList.Select(m => CleanModelName(m.Name)), "-----", .. bases];
        }
        RefinerModel = Register<T2IModel>(new("Refiner Model", "The model to use for refinement. This should be a model that's good at small-details, and use a structural model as your base model.\n'Use Base' will use your base model rather than switching.\nSDXL 1.0 released with an official refiner model.",
            "(Use Base)", IgnoreIf: "(Use Base)", GetValues: listRefinerModels, OrderPriority: -5, Group: GroupRefiners, FeatureFlag: "refiners", Subtype: "Stable-Diffusion", ChangeWeight: 9, DoNotPreview: true
            ));
        RefinerVAE = Register<T2IModel>(new("Refiner VAE", "Optional VAE replacement for the refiner stage.",
            "None", IgnoreIf: "None", GetValues: listVaes, IsAdvanced: true, OrderPriority: -4.5, Group: GroupRefiners, FeatureFlag: "refiners", Subtype: "VAE", ChangeWeight: 7, DoNotPreview: true
            ));
        RefinerControl = Register<double>(new("Refiner Control Percentage", "Higher values give the refiner more control, lower values give the base more control.\nThis is similar to 'Init Image Creativity', but for the refiner. This controls how many steps the refiner takes.",
            "0.2", Min: 0, Max: 1, Step: 0.05, OrderPriority: -4, ViewType: ParamViewType.SLIDER, Group: GroupRefiners, FeatureFlag: "refiners", DoNotPreview: true, Examples: ["0.2", "0.3", "0.4"]
            ));
        RefinerSteps = Register<int>(new("Refiner Steps", "Alternate Steps value for the refiner stage.",
            "20", Min: 1, Max: 200, ViewMax: 100, Step: 1, Examples: ["10", "15", "20", "30", "40"], OrderPriority: -3.75, Toggleable: true, IsAdvanced: true, Group: GroupRefiners, ViewType: ParamViewType.SLIDER
            ));
        RefinerMethod = Register<string>(new("Refiner Method", "How to apply the refiner. Different methods create different results.\n'PostApply' runs the base in full, then runs the refiner with an Init Image.\n'StepSwap' swaps the model after x steps during generation.\n'StepSwapNoisy' is StepSwap but with first-stage noise only.",
            "PostApply", GetValues: (_) => ["PostApply", "StepSwap", "StepSwapNoisy"], OrderPriority: -3, Group: GroupRefiners, FeatureFlag: "refiners", DoNotPreview: true, IsAdvanced: true
            ));
        RefinerUpscale = Register<double>(new("Refiner Upscale", "Optional upscale of the image between the base and refiner stage.\nSometimes referred to as 'high-res fix'.\nSetting to '1' disables the upscale.",
            "1", IgnoreIf: "1", Min: 0.25, Max: 8, ViewMax: 4, Step: 0.25, OrderPriority: -2, ViewType: ParamViewType.SLIDER, Group: GroupRefiners, FeatureFlag: "refiners", DoNotPreview: true, Examples: ["1", "1.5", "2"]
            ));
        RefinerDoTiling = Register<bool>(new("Refiner Do Tiling", "If enabled, do generation tiling in the refiner stage.\nThis can fix some visual artifacts from scaling, but also introduce others (eg seams).\nThis may take a while to run.\nRecommended for SD3 if upscaling.",
            "false", IgnoreIf: "false", OrderPriority: 5, Group: GroupRefiners, FeatureFlag: "refiners", DoNotPreview: true
            ));
        static List<string> listVaes(Session s)
        {
            return ["None", .. Program.T2IModelSets["VAE"].ListModelsFor(s).Select(m => CleanModelName(m.Name))];
        }
        for (int i = 1; i <= 3; i++)
        {
            string suffix = i switch { 1 => "", 2 => " Two", 3 => " Three", _ => "Error" };
            T2IParamGroup group = new($"ControlNet{suffix}", Toggles: true, Open: false, IsAdvanced: i != 1, OrderPriority: -1 + i * 0.1, Description: $"Guide your image generations with ControlNets.\n<a href=\"{Utilities.RepoDocsRoot}/Features/ControlNet.md\">See more docs here.</a>");
            Controlnets[i - 1] = new()
            {
                NameSuffix = suffix,
                Group = group,
                Image = Register<Image>(new($"ControlNet{suffix} Image Input", "The image to use as the input to ControlNet guidance.\nThis image will be preprocessed by the chosen preprocessor.\nIf ControlNet is enabled, but this input is not, Init Image will be used instead.",
                    null, Toggleable: true, FeatureFlag: "controlnet", Group: group, OrderPriority: 1, ChangeWeight: 2
                    )),
                Model = Register<T2IModel>(new($"ControlNet{suffix} Model", "The ControlNet model to use.",
                    "(None)", FeatureFlag: "controlnet", Group: group, Subtype: "ControlNet", OrderPriority: 5, ChangeWeight: 5
                    )),
                Strength = Register<double>(new($"ControlNet{suffix} Strength", "Higher values make the ControlNet apply more strongly. Weaker values let the prompt overrule the ControlNet.",
                    "1", FeatureFlag: "controlnet", Min: 0, Max: 2, Step: 0.05, OrderPriority: 8, ViewType: ParamViewType.SLIDER, Group: group, Examples: ["0", "0.5", "1", "2"]
                    )),
                Start = Register<double>(new($"ControlNet{suffix} Start", "When to start applying controlnet, as a fraction of steps.\nFor example, 0.5 starts applying halfway through. Must be less than End.\nExcluding early steps reduces the controlnet's impact on overall image structure.",
                    "0", IgnoreIf: "0", FeatureFlag: "controlnet", Min: 0, Max: 1, Step: 0.05, OrderPriority: 10, IsAdvanced: true, ViewType: ParamViewType.SLIDER, Group: group, Examples: ["0", "0.2", "0.5"]
                    )),
                End = Register<double>(new($"ControlNet{suffix} End", "When to stop applying controlnet, as a fraction of steps.\nFor example, 0.5 stops applying halfway through. Must be greater than Start.\nExcluding later steps reduces the controlnet's impact on finer details.",
                    "1", IgnoreIf: "1", FeatureFlag: "controlnet", Min: 0, Max: 1, Step: 0.05, OrderPriority: 11, IsAdvanced: true, ViewType: ParamViewType.SLIDER, Group: group, Examples: ["1", "0.8", "0.5"]
                    )),
            };
        }
        ControlNetPreviewOnly = Register<bool>(new("ControlNet Preview Only", "(For API usage) If enabled, requests preview output from ControlNet and no image generation at all.",
            "false", IgnoreIf: "false", FeatureFlag: "controlnet", VisibleNormally: false
            ));
        GroupVideo = new("Video", Open: false, OrderPriority: 0, Toggles: true, Description: $"Generate videos with Stable Video Diffusion.\n<a href=\"{Utilities.RepoDocsRoot}/Features/Video.md\">See more docs here.</a>");
        VideoModel = Register<T2IModel>(new("Video Model", "The model to use for video generation.\nThis should be an SVD (Stable Video Diffusion) model.\nNote that SVD favors a low CFG (~2.5).",
            "", GetValues: s => Program.MainSDModels.ListModelsFor(s).Where(m => m.ModelClass is not null && m.ModelClass.ID.Contains("stable-video-diffusion")).OrderBy(m => m.Name).Select(m => CleanModelName(m.Name)).ToList(),
            OrderPriority: 1, Group: GroupVideo, FeatureFlag: "video", Subtype: "Stable-Diffusion", ChangeWeight: 9, DoNotPreview: true
            ));
        VideoFrames = Register<int>(new("Video Frames", "How many frames to generate within the video.",
            "25", Min: 1, Max: 100, OrderPriority: 2, Group: GroupVideo, FeatureFlag: "video", DoNotPreview: true
            ));
        VideoFPS = Register<int>(new("Video FPS", "The FPS (frames per second) to use for video generation.\nThis configures the target FPS the video will try to generate for.",
            "6", Min: 1, Max: 1024, ViewMax: 30, ViewType: ParamViewType.SLIDER, OrderPriority: 2.5, Group: GroupVideo, FeatureFlag: "video", DoNotPreview: true
            ));
        VideoSteps = Register<int>(new("Video Steps", "How many steps to use for the video model.\nHigher step counts yield better quality, but much longer generation time.\n40 well get good quality, but 20 is sufficient as a basis.",
            "40", Min: 1, Max: 200, ViewMax: 100, ViewType: ParamViewType.SLIDER, OrderPriority: 3, Group: GroupVideo, FeatureFlag: "video", DoNotPreview: true
            ));
        VideoCFG = Register<double>(new("Video CFG", "The CFG Scale to use for video generation.\nVideos start with this CFG on the first frame, and then reduce to MinCFG (normally 1) by the end frame.\nSVD-XT normally uses 25 frames, and SVD (non-XT) 0.9 used 14 frames.",
            "2.5", Min: 1, Max: 100, ViewMax: 20, Step: 0.5, OrderPriority: 4, ViewType: ParamViewType.SLIDER, Group: GroupVideo, FeatureFlag: "video", DoNotPreview: true
            ));
        VideoMinCFG = Register<double>(new("Video Min CFG", "The minimum CFG to use for video generation.\nVideos start with max CFG on first frame, and then reduce to this CFG. Set to -1 to disable.",
            "1.0", Min: -1, Max: 100, ViewMax: 30, Step: 0.5, OrderPriority: 4.5, ViewType: ParamViewType.SLIDER, Group: GroupVideo, FeatureFlag: "video", IsAdvanced: true, DoNotPreview: true
            ));
        VideoMotionBucket = Register<int>(new("Video Motion Bucket", "Which trained 'motion bucket' to use for the video model.\nHigher values induce more motion. Most values should stay in the 100-200 range.\n127 is a good baseline, as it is the most common value in SVD's training set.",
            "127", Min: 1, Max: 1023, OrderPriority: 10, Group: GroupVideo, FeatureFlag: "video", IsAdvanced: true
            ));
        VideoAugmentationLevel = Register<double>(new("Video Augmentation Level", "How much noise to add to the init image.\nHigher values yield more motion.",
            "0.0", Min: 0, Max: 10, Step: 0.01, OrderPriority: 11, ViewType: ParamViewType.SLIDER, Group: GroupVideo, FeatureFlag: "video", IsAdvanced: true, DoNotPreview: true
            ));
        VideoBoomerang = Register<bool>(new("Video Boomerang", "Whether to boomerang (aka pingpong) the video.\nIf true, the video will play and then play again in reverse to enable smooth looping.",
            "false", IgnoreIf: "false", OrderPriority: 18, Group: GroupVideo, IsAdvanced: true, FeatureFlag: "video", DoNotPreview: true
            ));
        VideoResolution = Register<string>(new("Video Resolution", "What resolution/aspect the video should use.\n'Image Aspect, Model Res' uses the aspect-ratio of the image, but the pixel-count size of the model standard resolution.\n'Model Preferred' means use the model's exact resolution (eg 1024x576).\n'Image' means your input image resolution.",
            "Image Aspect, Model Res", GetValues: _ => ["Image Aspect, Model Res", "Model Preferred", "Image"], OrderPriority: 19, Group: GroupVideo, FeatureFlag: "video", DoNotPreview: true
            ));
        VideoFormat = Register<string>(new("Video Format", "What format to save videos in.",
            "webp", GetValues: _ => ["webp", "gif", "webm", "h264-mp4", "prores"], OrderPriority: 20, Group: GroupVideo, FeatureFlag: "video", DoNotPreview: true
            ));
        Model = Register<T2IModel>(new("Model", "What main checkpoint model should be used.",
            "", Permission: "param_model", VisibleNormally: false, Subtype: "Stable-Diffusion", ChangeWeight: 10
            ));
        VAE = Register<T2IModel>(new("VAE", "The VAE (Variational Auto-Encoder) controls the translation between images and latent space.\nIf your images look faded out, or glitched, you may have the wrong VAE.\nAll models have a VAE baked in by default, this option lets you swap to a different one if you want to.",
            "None", IgnoreIf: "None", Permission: "param_model", IsAdvanced: true, Toggleable: true, GetValues: listVaes, Subtype: "VAE", Group: GroupAdvancedModelAddons, ChangeWeight: 7
            ));
        Loras = Register<List<string>>(new("LoRAs", "LoRAs (Low-Rank-Adaptation Models) are a way to customize the content of a model without totally replacing it.\nYou can enable one or several LoRAs over top of one model.",
            "", IgnoreIf: "", IsAdvanced: true, Toggleable: true, Clean: (_, s) => CleanModelNameList(s), GetValues: (session) => Program.T2IModelSets["LoRA"].ListModelNamesFor(session).Order().Select(CleanModelName).ToList(), Group: GroupAdvancedModelAddons, VisibleNormally: false, ChangeWeight: 8
            ));
        LoraWeights = Register<List<string>>(new("LoRA Weights", "Weight values for the LoRA model list.\nComma separated list of weight numbers.\nMust match the length of the LoRAs input.",
            "", IgnoreIf: "", Min: -10, Max: 10, Step: 0.1, IsAdvanced: true, Toggleable: true, Group: GroupAdvancedModelAddons, VisibleNormally: false
            ));
        LoraSectionConfinement = Register<List<string>>(new("LoRA Section Confinement", "Optional internal parameter used to confine LoRAs to certain sections of generation (eg a 'segment' block).\nComma separated list of section IDs (0 to mean global).\nMust match the length of the LoRAs input.",
            "", IgnoreIf: "", IsAdvanced: true, Toggleable: true, Group: GroupAdvancedModelAddons, VisibleNormally: false
            ));
        GroupSwarmInternal = new("Swarm Internal", Open: false, OrderPriority: 0, IsAdvanced: true);
        BatchSize = Register<int>(new("Batch Size", "Batch size - generates more images at once on a single GPU.\nThis increases VRAM usage.\nMay in some cases increase overall speed by a small amount (runs slower to get the images, but slightly faster per-image).",
            "1", IgnoreIf: "1", Min: 1, Max: 100, Step: 1, IsAdvanced: true, ViewType: ParamViewType.SLIDER, ViewMax: 10, ChangeWeight: 2, Group: GroupSwarmInternal, OrderPriority: -20
            ));
        AltResolutionHeightMult = Register<double>(new("Alt Resolution Height Multiplier", "When enabled, the normal width parameter is used, and this value is multiplied by the width to derive the image height.",
            "1", Min: 0, Max: 10, Step: 0.1, Examples: ["0.5", "1", "1.5"], IsAdvanced: true, Toggleable: true, ViewType: ParamViewType.SLIDER, Group: GroupSwarmInternal, OrderPriority: -19
            ));
        SaveIntermediateImages = Register<bool>(new("Save Intermediate Images", "If checked, intermediate images (eg before a refiner or segment stage) will be saved separately alongside the final image.",
            "false", IgnoreIf: "false", IsAdvanced: true, Group: GroupSwarmInternal, OrderPriority: -16
            ));
        DoNotSave = Register<bool>(new("Do Not Save", "If checked, tells the server to not save this image.\nUseful for quick test generations, or 'generate forever' usage.",
            "false", IgnoreIf: "false", IsAdvanced: true, Group: GroupSwarmInternal, AlwaysRetain: true, OrderPriority: -15
            ));
        NoPreviews = Register<bool>(new("No Previews", "If checked, tells the server that previews are not desired.\nMay make generations slightly faster in some cases.",
            "false", IgnoreIf: "false", IsAdvanced: true, Group: GroupSwarmInternal, AlwaysRetain: true, OrderPriority: -14
            ));
        BackendType = Register<string>(new("[Internal] Backend Type", "Which StableSwarmUI backend type should be used for this request.",
            "Any", IgnoreIf: "Any", GetValues: (_) => ["Any", .. Program.Backends.BackendTypes.Keys],
            IsAdvanced: true, Permission: "param_backend_type", Group: GroupSwarmInternal, AlwaysRetain: true, OrderPriority: -10
            ));
        ExactBackendID = Register<int>(new("Exact Backend ID", "Manually force a specific exact backend (by ID #) to be used for this generation.",
            "0", Toggleable: true, IsAdvanced: true, ViewType: ParamViewType.BIG, Permission: "param_backend_id", Group: GroupSwarmInternal, AlwaysRetain: true, OrderPriority: -9
            ));
        WildcardSeed = Register<long>(new("Wildcard Seed", "Wildcard selection seed.\nIf enabled, this seed will be used for selecting entries from wildcards.\nIf disabled, the image seed will be used.\n-1 = random.",
            "-1", Min: -1, Max: uint.MaxValue, Step: 1, Toggleable: true, Examples: ["1", "2", "...", "10"], ViewType: ParamViewType.SEED, Group: GroupSwarmInternal, AlwaysRetain: true, ChangeWeight: -4, OrderPriority: -5
            ));
        NoSeedIncrement = Register<bool>(new("No Seed Increment", "If checked, the seed will not be incremented when Images is above 1.\nUseful for example to test different wildcards for the same seed rapidly.",
            "false", IgnoreIf: "false", IsAdvanced: true, Group: GroupSwarmInternal, AlwaysRetain: true, OrderPriority: -4
            ));
        RawResolution = Register<string>(new("Raw Resolution", "Optional advanced way to manually specify raw resolutions, useful for grids.\nWhen enabled, this overrides the default width/height params.",
            "1024x1204", Examples: ["512x512", "1024x1024", "1344x768"], Toggleable: true, IsAdvanced: true, Group: GroupSwarmInternal, OrderPriority: -3, Clean: (_, s) =>
            {
                (string widthText, string heightText) = s.BeforeAndAfter('x');
                int width = int.Parse(widthText.Trim());
                int height = int.Parse(heightText.Trim());
                if (width < 64 || height < 64 || width > 16384 || height > 16384)
                {
                    throw new InvalidDataException($"Invalid resolution: {width}x{height} (must be between 64x64 and 16384x16384)");
                }
                return s;
            }
            ));
        PersonalNote = Register<string>(new("Personal Note", "Optional field to type in any personal text note you want.\nThis will be stored in the image metadata.",
            "", IgnoreIf: "", IsAdvanced: true, Group: GroupSwarmInternal, ViewType: ParamViewType.BIG, AlwaysRetain: true, OrderPriority: 0
            ));
        ImageFormat = Register<string>(new("Image Format", "Optional override for the final image file format.",
            "PNG", GetValues: (_) => [.. Enum.GetNames(typeof(Image.ImageFormat))], IsAdvanced: true, Group: GroupSwarmInternal, AlwaysRetain: true, Toggleable: true, OrderPriority: 1
            ));
        ModelSpecificEnhancements = Register<bool>(new("Model Specific Enhancements", "If checked, enables model-specific enhancements.\nFor example, on SDXL, smarter res-cond will be used.\nIf unchecked, will prefer more 'raw' behavior.",
            "true", IgnoreIf: "true", IsAdvanced: true, Group: GroupSwarmInternal, OrderPriority: 2
            ));
        GroupFreeU = new("FreeU", Open: false, OrderPriority: 10, IsAdvanced: true, Toggles: true, Description: "<a class=\"translate\" href=\"https://arxiv.org/abs/2309.11497\">Implements 'FreeU: Free Lunch in Diffusion U-Net'</a>");
        FreeUApplyTo = Register<string>(new("[FreeU] Apply To", "Which models to apply FreeU to, as base, refiner, or both. Irrelevant when not using refiner.",
            "Both", GetValues: (_) => ["Both", "Base", "Refiner"], IsAdvanced: true, Group: GroupFreeU, FeatureFlag: "freeu"
            ));
        FreeUBlock1 = Register<double>(new("[FreeU] Block One", "Block1 multiplier value for FreeU.\nPaper recommends 1.1.",
            "1.1", Min: 0, Max: 10, Step: 0.05, IsAdvanced: true, Group: GroupFreeU, FeatureFlag: "freeu"
            ));
        FreeUBlock2 = Register<double>(new("[FreeU] Block Two", "Block2 multiplier value for FreeU.\nPaper recommends 1.2.",
            "1.2", Min: 0, Max: 10, Step: 0.05, IsAdvanced: true, Group: GroupFreeU, FeatureFlag: "freeu"
            ));
        FreeUSkip1 = Register<double>(new("[FreeU] Skip One", "Skip1 multiplier value for FreeU.\nPaper recommends 0.9.",
            "0.9", Min: 0, Max: 10, Step: 0.05, IsAdvanced: true, Group: GroupFreeU, FeatureFlag: "freeu"
            ));
        FreeUSkip2 = Register<double>(new("[FreeU] Skip Two", "Skip2 multiplier value for FreeU.\nPaper recommends 0.2.",
            "0.2", Min: 0, Max: 10, Step: 0.05, IsAdvanced: true, Group: GroupFreeU, FeatureFlag: "freeu"
            ));
        GroupRegionalPrompting = new("Regional Prompting", Open: false, OrderPriority: 5, IsAdvanced: true);
        GlobalRegionFactor = Register<double>(new("Global Region Factor", "When using regionalized prompts, this factor controls how strongly the global prompt overrides the regional prompts.\n0 means ignore global prompt, 1 means ignore regional, 0.5 means half-n-half.",
            "0.5", Toggleable: true, IgnoreIf: "0.5", Min: 0, Max: 1, Step: 0.05, ViewType: ParamViewType.SLIDER, Group: GroupRegionalPrompting, OrderPriority: -5
            ));
        RegionalObjectCleanupFactor = Register<double>(new("Regional Object Cleanup Factor", "When using an 'object' prompt, how much to cleanup the end result by.\nThis is the 'init image creativity' of the final cleanup step.\nSet to 0 to disable.",
            "0", IgnoreIf: "0", Min: 0, Max: 1, Step: 0.05, ViewType: ParamViewType.SLIDER, Group: GroupRegionalPrompting, OrderPriority: -4
            ));
        RegionalObjectInpaintingModel = Register<T2IModel>(new("Regional Object Inpainting Model", "When using regionalized prompts with distinct 'object' values, this overrides the model used to inpaint those objects.",
            "", Toggleable: true, Subtype: "Stable-Diffusion", Group: GroupRegionalPrompting, OrderPriority: -3
            ));
        SegmentModel = Register<T2IModel>(new("Segment Model", "Optionally specify a distinct model to use for 'segment' values.",
            "", Toggleable: true, Subtype: "Stable-Diffusion", Group: GroupRegionalPrompting, OrderPriority: 2
            ));
        SaveSegmentMask = Register<bool>(new("Save Segment Mask", "If checked, any usage of '<segment:>' syntax in prompts will save the generated mask in output.",
            "false", IgnoreIf: "false", Group: GroupRegionalPrompting, OrderPriority: 3
            ));
        SegmentMaskBlur = Register<int>(new("Segment Mask Blur", "Amount of blur to apply to the segment mask before using it.\nThis is for '<segment:>' syntax usage.\nDefaults to 10.",
            "10", Min: 0, Max: 64, Group: GroupRegionalPrompting, Examples: ["0", "4", "8", "16"], Toggleable: true, OrderPriority: 4
            ));
        SegmentMaskGrow = Register<int>(new("Segment Mask Grow", "Number of pixels of grow the segment mask by.\nThis is for '<segment:>' syntax usage.\nDefaults to 16.",
            "16", Min: 0, Max: 512, Group: GroupRegionalPrompting, Examples: ["0", "4", "8", "16", "32"], Toggleable: true, OrderPriority: 5
            ));
        SegmentThresholdMax = Register<double>(new("Segment Threshold Max", "Maximum mask match value of a segment before clamping.\nLower values force more of the mask to be counted as maximum masking.\nToo-low values may include unwanted areas of the image.\nHigher values may soften the mask.",
            "1", Min: 0.01, Max: 1, Step: 0.05, Toggleable: true, ViewType: ParamViewType.SLIDER, Group: GroupRegionalPrompting, OrderPriority: 6
            ));
        EndStepsEarly = Register<double>(new("End Steps Early", "Percentage of steps to cut off before the image is done generation.",
            "0", Toggleable: true, IgnoreIf: "0", VisibleNormally: false, Min: 0, Max: 1, FeatureFlag: "endstepsearly"
            ));
        GroupAdvancedSampling = new("Advanced Sampling", Open: false, OrderPriority: 10, IsAdvanced: true);
        SamplerSigmaMin = Register<double>(new("Sampler Sigma Min", "Minimum sigma value for the sampler.\nOnly applies to Karras/Exponential schedulers.",
            "0", Min: 0, Max: 1000, Step: 0.01, Toggleable: true, IsAdvanced: true, Group: GroupAdvancedSampling
            ));
        SamplerSigmaMax = Register<double>(new("Sampler Sigma Max", "Maximum sigma value for the sampler.\nOnly applies to Karras/Exponential schedulers.",
            "10", Min: 0, Max: 1000, Step: 0.01, Toggleable: true, IsAdvanced: true, Group: GroupAdvancedSampling, FeatureFlag: "sd3"
            ));
        SigmaShift = Register<double>(new("Sigma Shift", "Sigma shift is used for SD3 models specifically.\nThis value is recommended to be in the range of 1.5 to 3, normally 3.",
            "3", Min: 0, Max: 100, Step: 0.01, Toggleable: true, IsAdvanced: true, Group: GroupAdvancedSampling
            ));
        SamplerRho = Register<double>(new("Sampler Rho", "Rho value for the sampler.\nOnly applies to Karras/Exponential schedulers.",
            "7", Min: 0, Max: 1000, Step: 0.01, Toggleable: true, IsAdvanced: true, Group: GroupAdvancedSampling
            ));
        IP2PCFG2 = Register<double>(new("IP2P CFG 2", "CFG Scale for Cond2-Negative in InstructPix2Pix (Edit) models.",
            "1.5", Toggleable: true, Min: 1, Max: 100, ViewMax: 20, Step: 0.5, Examples: ["1.5", "2"], ViewType: ParamViewType.SLIDER, Group: GroupAdvancedSampling
            ));
        ClipStopAtLayer = Register<int>(new("CLIP Stop At Layer", "What layer of CLIP to stop at, from the end.\nAlso known as 'CLIP Skip'. Default CLIP Skip is -1 for SDv1, some models prefer -2.\nSDv2, SDXL, and beyond do not need this set ever.",
            "-1", Min: -24, Max: -1, Step: 1, Toggleable: true, IsAdvanced: true, Group: GroupAdvancedSampling
            ));
        CascadeLatentCompression = Register<int>(new("Cascade Latent Compression", "How deeply to compress latents when using Stable Cascade.\nDefault is 32, you can get slightly faster but lower quality results by using 42.",
            "32", IgnoreIf: "32", Min: 1, Max: 100, Step: 1, IsAdvanced: true, Group: GroupAdvancedSampling
            ));
        VAETileSize = Register<int>(new("VAE Tile Size", "If enabled, decodes images through the VAE using tiles of this size.\nVAE Tiling reduces VRAM consumption, but takes longer and may impact quality.",
            "512", Min: 320, Max: 4096, Step: 64, Toggleable: true, IsAdvanced: true, Group: GroupAdvancedSampling
            ));
        RemoveBackground = Register<bool>(new("Remove Background", "If enabled, removes the background from the generated image.\nThis uses RemBG.",
            "false", IgnoreIf: "false", IsAdvanced: true, Group: GroupAdvancedSampling
             ));
    }

    /// <summary>Gets the value in the list that best matches the input text of a model name (for user input handling), or null if no match.</summary>
    public static string GetBestModelInList(string name, IEnumerable<string> list)
    {
        return GetBestInList(name, list, s => CleanNameGeneric(CleanModelName(s)));
    }

    /// <summary>Gets the value in the list that best matches the input text (for user input handling), or null if no match.</summary>
    public static string GetBestInList(string name, IEnumerable<string> list, Func<string, string> cleanFunc = null)
    {
        cleanFunc ??= CleanNameGeneric;
        string backup = null;
        int bestLen = 999;
        name = cleanFunc(name);
        foreach (string listVal in list)
        {
            string listValClean = cleanFunc(listVal);
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
        string origVal = val;
        if (type is null)
        {
            throw new InvalidDataException("Unknown parameter type");
        }
        switch (type.Type)
        {
            case T2IParamDataType.INTEGER:
                if (!long.TryParse(val, out long valInt))
                {
                    throw new InvalidDataException($"Invalid integer value for param {type.Name} - '{origVal}' - must be a valid integer (eg '0', '3', '-5', etc)");
                }
                if (type.Min != 0 || type.Max != 0)
                {
                    if (valInt < type.Min || valInt > type.Max)
                    {
                        throw new InvalidDataException($"Invalid integer value for param {type.Name} - '{origVal}' - must be between {type.Min} and {type.Max}");
                    }
                }
                return valInt.ToString();
            case T2IParamDataType.DECIMAL:
                if (!double.TryParse(val, out double valDouble))
                {
                    throw new InvalidDataException($"Invalid decimal value for param {type.Name} - '{origVal}' - must be a valid decimal (eg '0.0', '3.5', '-5.2', etc)");
                }
                if (type.Min != 0 || type.Max != 0)
                {
                    if (valDouble < type.Min || valDouble > type.Max)
                    {
                        throw new InvalidDataException($"Invalid decimal value for param {type.Name} - '{origVal}' - must be between {type.Min} and {type.Max}");
                    }
                }
                return valDouble.ToString();
            case T2IParamDataType.BOOLEAN:
                val = val.ToLowerFast();
                if (val != "true" && val != "false")
                {
                    throw new InvalidDataException($"Invalid boolean value for param {type.Name} - '{origVal}' - must be exactly 'true' or 'false'");
                }
                return val;
            case T2IParamDataType.TEXT:
            case T2IParamDataType.DROPDOWN:
                if (type.GetValues is not null && type.ValidateValues)
                {
                    val = GetBestInList(val, type.GetValues(session));
                    if (val is null)
                    {
                        throw new InvalidDataException($"Invalid value for param {type.Name} - '{origVal}' - must be one of: `{string.Join("`, `", type.GetValues(session))}`");
                    }
                }
                return val;
            case T2IParamDataType.LIST:
                string[] vals = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (type.GetValues is not null && type.ValidateValues)
                {
                    List<string> possible = type.GetValues(session);
                    for (int i = 0; i < vals.Length; i++)
                    {
                        string search = vals[i];
                        vals[i] = GetBestInList(search, possible);
                        if (vals[i] is null)
                        {
                            vals[i] = GetBestModelInList(CleanModelName(search), possible);
                            if (vals[i] is null)
                            {
                                throw new InvalidDataException($"Invalid value for param {type.Name} - '{origVal}' - must be one of: `{string.Join("`, `", type.GetValues(session))}`");
                            }
                        }
                    }
                    return vals.JoinString(",");
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
                    string shortText = val.Length > 10 ? val[..10] + "..." : val;
                    throw new InvalidDataException($"Invalid image value for param {type.Name} - '{origVal}' - must be a valid base64 string - got '{shortText}'");
                }
                return val;
            case T2IParamDataType.IMAGE_LIST:
                List<string> parts = [];
                foreach (string part in val.Split('|'))
                {
                    string partVal = part.Trim();
                    if (partVal.StartsWith("data:"))
                    {
                        partVal = partVal.After(',');
                    }
                    if (string.IsNullOrWhiteSpace(val))
                    {
                        continue;
                    }
                    if (!ValidBase64Matcher.IsOnlyMatches(partVal) || partVal.Length < 10)
                    {
                        string shortText = partVal.Length > 10 ? partVal[..10] + "..." : partVal;
                        throw new InvalidDataException($"Invalid image-list value for param {type.Name} - '{origVal}' - must be a valid base64 string - got '{shortText}'");
                    }
                    parts.Add(partVal);
                }
                return parts.JoinString("|");
            case T2IParamDataType.MODEL:
                if (!Program.T2IModelSets.TryGetValue(type.Subtype ?? "Stable-Diffusion", out T2IModelHandler handler))
                {
                    throw new InvalidDataException($"Invalid model sub-type for param {type.Name}: '{type.Subtype}' - are you sure that type name is correct? (Developer error)");
                }
                val = GetBestModelInList(val, [.. handler.ListModelNamesFor(session)]);
                if (val is null)
                {
                    throw new InvalidDataException($"Invalid model value for param {type.Name} - '{origVal}' - are you sure that model name is correct?");
                }
                return val;
        }
        throw new InvalidDataException($"Unknown parameter type's data type? {type.Type}");
    }

    /// <summary>Takes user input of a parameter and applies it to the parameter tracking data object.</summary>
    public static void ApplyParameter(string paramTypeName, string value, T2IParamInput data)
    {
        if (!TryGetType(paramTypeName, out T2IParamType type, data))
        {
            throw new InvalidDataException("Unrecognized parameter type name.");
        }
        if (value == type.IgnoreIf)
        {
            return;
        }
        if (type.Permission is not null)
        {
            if (!data.SourceSession.User.HasGenericPermission(type.Permission))
            {
                throw new InvalidDataException($"You do not have permission to use parameter {type.Name}.");
            }
        }
        try
        {
            value = ValidateParam(type, value, data.SourceSession);
            data.Set(type, value);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"Invalid value for parameter {type.Name}: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Invalid value for parameter {type.Name}", ex);
        }
    }

    /// <summary>Gets the type data for a given type name.</summary>
    public static T2IParamType GetType(string name, T2IParamInput context)
    {
        name = CleanTypeName(name);
        T2IParamType result = Types.GetValueOrDefault(name);
        if (result is not null)
        {
            return result;
        }
        foreach (Func<string, T2IParamInput, T2IParamType> provider in FakeTypeProviders)
        {
            result = provider(name, context);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }

    /// <summary>Tries to get the type data for a given type name, returning whether it was found, and outputting the type if it was found.</summary>
    public static bool TryGetType(string name, out T2IParamType type, T2IParamInput context)
    {
        name = CleanTypeName(name);
        type = GetType(name, context);
        return type is not null;
    }

    /// <summary>Gets the actual width,height value for a given aspect ratio, based on a 512x512 base scale.</summary>
    public static (int, int) AspectRatioToSizeReference(string aspectRatio)
    {
        int width, height;
        if (aspectRatio == "1:1") { width = 512; height = 512; }
        else if (aspectRatio == "4:3") { width = 576; height = 448; }
        else if (aspectRatio == "3:2") { width = 608; height = 416; }
        else if (aspectRatio == "8:5") { width = 608; height = 384; }
        else if (aspectRatio == "16:9") { width = 672; height = 384; }
        else if (aspectRatio == "21:9") { width = 768; height = 320; }
        else if (aspectRatio == "3:4") { width = 448; height = 576; }
        else if (aspectRatio == "2:3") { width = 416; height = 608; }
        else if (aspectRatio == "5:8") { width = 384; height = 608; }
        else if (aspectRatio == "9:16") { width = 384; height = 672; }
        else if (aspectRatio == "9:21") { width = 320; height = 768; }
        else { width = -1; height = -1; }
        return (width, height);
    }
}
