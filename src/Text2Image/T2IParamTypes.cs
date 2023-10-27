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
    /// <summary>Large numeric input box.</summary>
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
/// <param name="Type">The type of the type - text vs integer vs etc (will be set when registering).</param>
/// <param name="DoNotSave">Can be set to forbid tracking/saving of a param value.</param>
/// <param name="ImageShouldResize">(For Image-type params) If true, the image should resize to match the target resolution.</param>
/// <param name="Subtype">The sub-type of the type - for models, this might be eg "Stable-Diffusion".</param>
/// <param name="ID">The raw ID of this parameter (will be set when registering).</param>
/// <param name="SharpType">The C# datatype.</param>
/// 
public record class T2IParamType(string Name, string Description, string Default, double Min = 0, double Max = 0, double Step = 1, double ViewMax = 0,
    Func<string, string, string> Clean = null, Func<Session, List<string>> GetValues = null, string[] Examples = null, Func<List<string>, List<string>> ParseList = null, bool ValidateValues = true,
    bool VisibleNormally = true, bool IsAdvanced = false, string FeatureFlag = null, string Permission = null, bool Toggleable = false, double OrderPriority = 10, T2IParamGroup Group = null, string IgnoreIf = null,
    ParamViewType ViewType = ParamViewType.SMALL, bool HideFromMetadata = false, Func<string, string> MetadataFormat = null, bool AlwaysRetain = false,
    T2IParamDataType Type = T2IParamDataType.UNSET, bool DoNotSave = false, bool ImageShouldResize = true, string Subtype = null, string ID = null, Type SharpType = null)
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
            ["view_type"] = ViewType.ToString().ToLowerFast()
        };
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
/// <param name="IsAdvanced">If 'false', this is an advanced setting group that should be hidden by a dropdown.</param>
public record class T2IParamGroup(string Name, bool Toggles = false, bool Open = true, double OrderPriority = 10, string Description = "", bool IsAdvanced = false)
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

    /// <summary>Register a new parameter type.</summary>
    public static T2IRegisteredParam<T> Register<T>(T2IParamType type)
    {
        type = type with { ID = CleanTypeName(type.Name), Type = SharpTypeToDataType(typeof(T), type.GetValues != null), SharpType = typeof(T) };
        Types.Add(type.ID, type);
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

    /// <summary>Applies a string edit, with support for "{value}" notation.</summary>
    public static string ApplyStringEdit(string prior, string update)
    {
        if (update.Contains("{value}"))
        {
            return update.Replace("{value}", prior ?? "");
        }
        return update;
    }

    public static T2IRegisteredParam<string> Prompt, NegativePrompt, AspectRatio, BackendType, RefinerMethod, FreeUApplyTo;
    public static T2IRegisteredParam<int> Images, Steps, Width, Height, BatchSize, ExactBackendID, VAETileSize;
    public static T2IRegisteredParam<long> Seed, VariationSeed;
    public static T2IRegisteredParam<double> CFGScale, VariationSeedStrength, InitImageCreativity, RefinerControl, RefinerUpscale, ControlNetStrength, ReVisionStrength, AltResolutionHeightMult,
        FreeUBlock1, FreeUBlock2, FreeUSkip1, FreeUSkip2, GlobalRegionFactor, EndStepsEarly, SamplerSigmaMin, SamplerSigmaMax, SamplerRho;
    public static T2IRegisteredParam<Image> InitImage, MaskImage, ControlNetImage;
    public static T2IRegisteredParam<T2IModel> Model, RefinerModel, VAE, ControlNetModel, ReVisionModel, RegionalObjectInpaintingModel;
    public static T2IRegisteredParam<List<string>> Loras, LoraWeights;
    public static T2IRegisteredParam<List<Image>> PromptImages;
    public static T2IRegisteredParam<bool> DoNotSave, ControlNetPreviewOnly, RevisionZeroPrompt, SeamlessTileable;

    public static T2IParamGroup GroupRevision, GroupCore, GroupVariation, GroupResolution, GroupInitImage, GroupRefiners, GroupControlNet,
        GroupAdvancedModelAddons, GroupSwarmInternal, GroupFreeU, GroupRegionalPrompting, GroupAdvancedSampling;

    /// <summary>(For extensions) list of functions that provide fake types for given type names.</summary>
    public static List<Func<string, T2IParamInput, T2IParamType>> FakeTypeProviders = new();

    /// <summary>(Called by <see cref="Program"/> during startup) registers all default parameter types.</summary>
    public static void RegisterDefaults()
    {
        Prompt = Register<string>(new("Prompt", "The input prompt text that describes the image you want to generate.\nTell the AI what you want to see.",
            "", Clean: ApplyStringEdit, Examples: new[] { "a photo of a cat", "a cartoonish drawing of an astronaut" }, OrderPriority: -100, VisibleNormally: false, ViewType: ParamViewType.PROMPT
            ));
        PromptImages = Register<List<Image>>(new("Prompt Images", "Images to include with the prompt, for eg ReVision or UnCLIP.",
            "", IgnoreIf: "", OrderPriority: -95, Toggleable: true, VisibleNormally: false, IsAdvanced: true, ImageShouldResize: false, HideFromMetadata: true // Has special internal handling
            ));
        GroupAdvancedModelAddons = new("Advanced Model Addons", Open: false, IsAdvanced: true);
        NegativePrompt = Register<string>(new("Negative Prompt", "Like the input prompt text, but describe what NOT to generate.\nTell the AI things you don't want to see.",
            "", IgnoreIf: "", Clean: ApplyStringEdit, Examples: new[] { "ugly, bad, gross", "lowres, low quality" }, OrderPriority: -90, ViewType: ParamViewType.PROMPT
            ));
        GroupRevision = new("ReVision", Open: false, Toggles: true, OrderPriority: -70);
        ReVisionStrength = Register<double>(new("ReVision Strength", "How strong to apply ReVision image inputs.",
            "1", OrderPriority: -70, Min: 0, Max: 10, Step: 0.1, ViewType: ParamViewType.SLIDER, Group: GroupRevision
            ));
        RevisionZeroPrompt = Register<bool>(new("ReVision Zero Prompt", "Zeroes the prompt and negative prompt for ReVision inputs.\nApplies only to the base, the refiner will still get prompts.\nIf you want zeros on both, just delete your prompt text."
            + "\nIf not checked, empty prompts will be zeroed regardless.",
            "false", IgnoreIf: "false", Group: GroupRevision
            ));
        ReVisionModel = Register<T2IModel>(new("ReVision Model", "The CLIP Vision model to use for ReVision inputs.",
            "", Subtype: "ClipVision", IsAdvanced: true, Toggleable: true, Group: GroupAdvancedModelAddons
            ));
        GroupCore = new("Core Parameters", Toggles: false, Open: true, OrderPriority: -50);
        Images = Register<int>(new("Images", "How many images to generate at once.",
            "1", IgnoreIf: "1", Min: 1, Max: 10000, Step: 1, Examples: new[] { "1", "4" }, OrderPriority: -50, Group: GroupCore
            ));
        Seed = Register<long>(new("Seed", "Image seed.\n-1 = random.\nDifferent seeds produce different results for the same prompt.",
            "-1", Min: -1, Max: long.MaxValue, Step: 1, Examples: new[] { "1", "2", "...", "10" }, OrderPriority: -30, ViewType: ParamViewType.SEED, Group: GroupCore
            ));
        Steps = Register<int>(new("Steps", "How many times to run the model.\nMore steps = better quality, but more time.\n20 is a good baseline for speed, 40 is good for maximizing quality.\nYou can go much higher, but it quickly becomes pointless above 70 or so.",
            "20", Min: 1, Max: 200, ViewMax: 100, Step: 1, Examples: new[] { "10", "15", "20", "30", "40" }, OrderPriority: -20, Group: GroupCore, ViewType: ParamViewType.SLIDER
            ));
        CFGScale = Register<double>(new("CFG Scale", "How strongly to scale prompt input.\nHigher CFG scales tend to produce more contrast, and lower CFG scales produce less contrast.\n"
            + "Too-high values can cause corrupted/burnt images, too-low can cause nonsensical images.\n7 is a good baseline. Normal usages vary between 5 and 9.",
            "7", Min: 0, Max: 100, ViewMax: 30, Step: 0.25, Examples: new[] { "5", "6", "7", "8", "9" }, OrderPriority: -18, ViewType: ParamViewType.SLIDER, Group: GroupCore
            ));
        GroupVariation = new("Variation Seed", Toggles: true, Open: false, OrderPriority: -17);
        VariationSeed = Register<long>(new("Variation Seed", "Image-variation seed.\nCombined partially with the original seed to create a similar-but-different image for the same seed.\n-1 = random.",
            "-1", Min: -1, Max: uint.MaxValue, Step: 1, Examples: new[] { "1", "2", "...", "10" }, OrderPriority: -17, ViewType: ParamViewType.SEED, Group: GroupVariation, FeatureFlag: "variation_seed"
            ));
        VariationSeedStrength = Register<double>(new("Variation Seed Strength", "How strongly to apply the variation seed.\n0 = don't use, 1 = replace the base seed entirely. 0.5 is a good value.",
            "0", IgnoreIf: "0", Min: 0, Max: 1, Step: 0.05, Examples: new[] { "0", "0.25", "0.5", "0.75" }, OrderPriority: -17, ViewType: ParamViewType.SLIDER, Group: GroupVariation, FeatureFlag: "variation_seed"
            ));
        GroupResolution = new("Resolution", Toggles: false, Open: false, OrderPriority: -11);
        AspectRatio = Register<string>(new("Aspect Ratio", "Image aspect ratio. Some models can stretch better than others.",
            "1:1", IgnoreIf: "Custom", GetValues: (_) => new() { "1:1", "4:3", "3:2", "8:5", "16:9", "21:9", "3:4", "2:3", "5:8", "9:16", "9:21", "Custom" }, OrderPriority: -11, Group: GroupResolution
            ));
        Width = Register<int>(new("Width", "Image width, in pixels.\nSDv1 uses 512, SDv2 uses 768, SDXL prefers 1024.\nSome models allow variation within a range (eg 512 to 768) but almost always want a multiple of 64.",
            "512", Min: 128, Max: 4096, Step: 64, Examples: new[] { "512", "768", "1024" }, OrderPriority: -10, ViewType: ParamViewType.POT_SLIDER, Group: GroupResolution
            ));
        Height = Register<int>(new("Height", "Image height, in pixels.\nSDv1 uses 512, SDv2 uses 768, SDXL prefers 1024.\nSome models allow variation within a range (eg 512 to 768) but almost always want a multiple of 64.",
            "512", Min: 128, Max: 4096, Step: 64, Examples: new[] { "512", "768", "1024" }, OrderPriority: -9, ViewType: ParamViewType.POT_SLIDER, Group: GroupResolution
            ));
        GroupInitImage = new("Init Image", Toggles: true, Open: false, OrderPriority: -5);
        InitImage = Register<Image>(new("Init Image", "Init-image, to edit an image using diffusion.\nThis process is sometimes called 'img2img' or 'Image To Image'.",
            "", OrderPriority: -5, Group: GroupInitImage
            ));
        InitImageCreativity = Register<double>(new("Init Image Creativity", "Higher values make the generation more creative, lower values follow the init image closer.\nSometimes referred to as 'Denoising Strength' for 'img2img'.",
            "0.6", Min: 0, Max: 1, Step: 0.05, OrderPriority: -4.5, ViewType: ParamViewType.SLIDER, Group: GroupInitImage
            ));
        MaskImage = Register<Image>(new("Mask Image", "Mask-image, white pixels are changed, black pixels are not changed, gray pixels are half-changed.",
            "", OrderPriority: -4, Group: GroupInitImage
            ));
        GroupRefiners = new("Refiner", Toggles: true, Open: false, OrderPriority: -3);
        static List<string> listRefinerModels(Session s)
        {
            List<T2IModel> baseList = Program.MainSDModels.ListModelsFor(s).OrderBy(m => m.Name).ToList();
            List<T2IModel> refinerList = baseList.Where(m => m.ModelClass is not null && m.ModelClass.Name.Contains("Refiner")).ToList();
            List<string> bases = baseList.Select(m => m.Name).ToList();
            if (refinerList.IsEmpty())
            {
                return bases;
            }
            return refinerList.Select(m => m.Name).Append("-----").Concat(bases).ToList();
        }
        RefinerModel = Register<T2IModel>(new("Refiner Model", "The model to use for refinement. This should be a model that's good at small-details, and use a structural model as your base model.\nSDXL 1.0 released with an official refiner model.",
            "", GetValues: listRefinerModels, OrderPriority: -5, Group: GroupRefiners, FeatureFlag: "refiners", Toggleable: true, Subtype: "Stable-Diffusion"
            ));
        RefinerControl = Register<double>(new("Refine Control Percentage", "Higher values give the refiner more control, lower values give the base more control.\nThis is similar to 'Init Image Creativity', but for the refiner. This controls how many steps the refiner takes.",
            "0.2", Min: 0, Max: 1, Step: 0.05, OrderPriority: -4, ViewType: ParamViewType.SLIDER, Group: GroupRefiners, FeatureFlag: "refiners"
            ));
        RefinerMethod = Register<string>(new("Refiner Method", "How to apply the refiner. Different methods create different results.\n'PostApply' runs the base in full, then runs the refiner with an Init Image.\n'StepSwap' swaps the model after x steps during generation.\n'StepSwapNoisy' is StepSwap but with first-stage noise only.",
            "PostApply", GetValues: (_) => new() { "PostApply", "StepSwap", "StepSwapNoisy" }, OrderPriority: -3, Group: GroupRefiners, FeatureFlag: "refiners"
            ));
        RefinerUpscale = Register<double>(new("Refiner Upscale", "Optional upscale of the image between the base and refiner stage.\nSometimes referred to as 'high-res fix'.\nSetting to '1' disables the upscale.",
            "1", IgnoreIf: "1", Min: 1, Max: 4, Step: 0.25, OrderPriority: -2, ViewType: ParamViewType.SLIDER, Group: GroupRefiners, FeatureFlag: "refiners"
            ));
        GroupControlNet = new("ControlNet", Toggles: true, Open: false, OrderPriority: -1);
        ControlNetImage = Register<Image>(new("ControlNet Image Input", "The image to use as the input to ControlNet guidance.\nThis image will be preprocessed by the chosen preprocessor.\nIf ControlNet is enabled, but this input is not, Init Image will be used instead.",
            "", Toggleable: true, FeatureFlag: "controlnet", Group: GroupControlNet, OrderPriority: 1
            ));
        ControlNetModel = Register<T2IModel>(new("ControlNet Model", "The ControlNet model to use.",
            "", FeatureFlag: "controlnet", Group: GroupControlNet, Subtype: "ControlNet", OrderPriority: 5
            ));
        ControlNetStrength = Register<double>(new("ControlNet Strength", "Higher values make the ControlNet apply more strongly. Weaker values let the prompt overrule the ControlNet.",
            "1", FeatureFlag: "controlnet", Min: 0, Max: 2, Step: 0.05, OrderPriority: 8, ViewType: ParamViewType.SLIDER, Group: GroupControlNet
            ));
        ControlNetPreviewOnly = Register<bool>(new("ControlNet Preview Only", "(For API usage) If enabled, requests preview output from ControlNet and no image generation at all.",
            "false", IgnoreIf: "false", FeatureFlag: "controlnet", VisibleNormally: false
            ));
        Model = Register<T2IModel>(new("Model", "What main checkpoint model should be used.",
            "", Permission: "param_model", VisibleNormally: false, Subtype: "Stable-Diffusion"
            ));
        VAE = Register<T2IModel>(new("VAE", "The VAE (Variational Auto-Encoder) controls the translation between images and latent space.\nIf your images look faded out, or glitched, you may have the wrong VAE.\nAll models have a VAE baked in by default, this option lets you swap to a different one if you want to.",
            "", IgnoreIf: "", Permission: "param_model", IsAdvanced: true, Toggleable: true, Subtype: "VAE", Group: GroupAdvancedModelAddons
            ));
        Loras = Register<List<string>>(new("LoRAs", "LoRAs (Low-Rank-Adaptation Models) are a way to customize the content of a model without totally replacing it.\nYou can enable one or several LoRAs over top of one model.",
            "", IgnoreIf: "", IsAdvanced: true, Toggleable: true, GetValues: (session) => Program.T2IModelSets["LoRA"].ListModelNamesFor(session).Order().ToList(), Group: GroupAdvancedModelAddons, VisibleNormally: false
            ));
        LoraWeights = Register<List<string>>(new("LoRA Weights", "Weight values for the LoRA model list.",
            "", IgnoreIf: "", IsAdvanced: true, Toggleable: true, Group: GroupAdvancedModelAddons, VisibleNormally: false
            ));
        AltResolutionHeightMult = Register<double>(new("Alt Resolution Height Multiplier", "When enabled, the normal width parameter is used, and this value is multiplied by the width to derive the image height.",
            "1", Min: 0, Max: 10, Step: 0.1, Examples: new[] { "0.5", "1", "1.5" }, IsAdvanced: true, Toggleable: true, ViewType: ParamViewType.SLIDER
            ));
        BatchSize = Register<int>(new("Batch Size", "Batch size - generates more images at once on a single GPU.\nThis increases VRAM usage.\nMay in some cases increase overall speed by a small amount (runs slower to get the images, but slightly faster per-image).",
            "1", IgnoreIf: "1", Min: 1, Max: 100, Step: 1, IsAdvanced: true, ViewType: ParamViewType.SLIDER, ViewMax: 10
            ));
        GroupSwarmInternal = new("Swarm Internal", Open: false, OrderPriority: 0, IsAdvanced: true);
        DoNotSave = Register<bool>(new("Do Not Save", "If checked, tells the server to not save this image.\nUseful for quick test generations, or 'generate forever' usage.",
            "false", IgnoreIf: "false", IsAdvanced: true, Group: GroupSwarmInternal, AlwaysRetain: true
            ));
        BackendType = Register<string>(new("[Internal] Backend Type", "Which StableSwarmUI backend type should be used for this request.",
            "Any", IgnoreIf: "Any", GetValues: (_) => new string[] { "Any" }.Concat(Program.Backends.BackendTypes.Keys).ToList(), IsAdvanced: true, Permission: "param_backend_type", Group: GroupSwarmInternal, AlwaysRetain: true
            ));
        ExactBackendID = Register<int>(new("Exact Backend ID", "Manually force a specific exact backend (by ID #) to be used for this generation.",
            "0", Toggleable: true, IsAdvanced: true, ViewType: ParamViewType.BIG, Permission: "param_backend_id", Group: GroupSwarmInternal, AlwaysRetain: true
            ));
        GroupFreeU = new("FreeU", Open: false, OrderPriority: 10, IsAdvanced: true, Toggles: true, Description: "Implements 'FreeU: Free Lunch in Diffusion U-Net' https://arxiv.org/abs/2309.11497");
        FreeUApplyTo = Register<string>(new("[FreeU] Apply To", "Which models to apply FreeU to, as base, refiner, or both. Irrelevant when not using refiner.",
            "Both", GetValues: (_) => new() { "Both", "Base", "Refiner" }, IsAdvanced: true, Group: GroupFreeU, FeatureFlag: "freeu"
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
            "0.5", Toggleable: true, IgnoreIf: "0.5", Min: 0, Max: 1, Step: 0.05, ViewType: ParamViewType.SLIDER, Group: GroupRegionalPrompting
            ));
        RegionalObjectInpaintingModel = Register<T2IModel>(new("Regional Object Inpainting Model", "When using regionalized prompts with distinct 'object' values, this overrides the model used to inpaint those objects.",
            "", Toggleable: true, Subtype: "Stable-Diffusion", Group: GroupRegionalPrompting
            ));
        EndStepsEarly = Register<double>(new("End Steps Early", "Percentage of steps to cut off before the image is done generation.",
            "0", Toggleable: true, IgnoreIf: "0", VisibleNormally: false, Min: 0, Max: 1, FeatureFlag: "endstepsearly"
            ));
        GroupAdvancedSampling = new("Advanced Sampling", Open: false, OrderPriority: 10, IsAdvanced: true);
        SamplerSigmaMin = Register<double>(new("Sampler Sigma Min", "Minimum sigma value for the sampler.\nOnly applies to Karras/Exponential schedulers.",
            "0", Min: 0, Max: 1000, Step: 0.01, Toggleable: true, IsAdvanced: true, Group: GroupAdvancedSampling
            ));
        SamplerSigmaMax = Register<double>(new("Sampler Sigma Max", "Maximum sigma value for the sampler.\nOnly applies to Karras/Exponential schedulers.",
            "10", Min: 0, Max: 1000, Step: 0.01, Toggleable: true, IsAdvanced: true, Group: GroupAdvancedSampling
            ));
        SamplerRho = Register<double>(new("Sampler Rho", "Rho value for the sampler.\nOnly applies to Karras/Exponential schedulers.",
            "7", Min: 0, Max: 1000, Step: 0.01, Toggleable: true, IsAdvanced: true, Group: GroupAdvancedSampling
            ));
        SeamlessTileable = Register<bool>(new("Seamless Tileable", "Makes the generated image seamlessly tileable (like a 3D texture would be).",
            "false", IgnoreIf: "false", IsAdvanced: true, Group: GroupAdvancedSampling, FeatureFlag: "seamless"
            ));
        VAETileSize = Register<int>(new("VAE Tile Size", "If enabled, decodes images through the VAE using tiles of this size.\nVAE Tiling reduces VRAM consumption, but takes longer and may impact quality.",
            "512", Min: 320, Max: 4096, Step: 64, Toggleable: true, IsAdvanced: true, Group: GroupAdvancedSampling
            ));
    }

    /// <summary>Gets the value in the list that best matches the input text (for user input handling).</summary>
    public static string GetBestInList(string name, IEnumerable<string> list)
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
                    throw new InvalidDataException($"Invalid integer value for param {type.Name} - '{val}' - must be a valid integer (eg '0', '3', '-5', etc)");
                }
                if (type.Min != 0 || type.Max != 0)
                {
                    if (valInt < type.Min || valInt > type.Max)
                    {
                        throw new InvalidDataException($"Invalid integer value for param {type.Name} - '{val}' - must be between {type.Min} and {type.Max}");
                    }
                }
                return valInt.ToString();
            case T2IParamDataType.DECIMAL:
                if (!double.TryParse(val, out double valDouble))
                {
                    throw new InvalidDataException($"Invalid decimal value for param {type.Name} - '{val}' - must be a valid decimal (eg '0.0', '3.5', '-5.2', etc)");
                }
                if (type.Min != 0 || type.Max != 0)
                {
                    if (valDouble < type.Min || valDouble > type.Max)
                    {
                        throw new InvalidDataException($"Invalid decimal value for param {type.Name} - '{val}' - must be between {type.Min} and {type.Max}");
                    }
                }
                return valDouble.ToString();
            case T2IParamDataType.BOOLEAN:
                val = val.ToLowerFast();
                if (val != "true" && val != "false")
                {
                    throw new InvalidDataException($"Invalid boolean value for param {type.Name} - '{val}' - must be exactly 'true' or 'false'");
                }
                return val;
            case T2IParamDataType.TEXT:
            case T2IParamDataType.DROPDOWN:
                if (type.GetValues is not null && type.ValidateValues)
                {
                    val = GetBestInList(val, type.GetValues(session));
                    if (val is null)
                    {
                        throw new InvalidDataException($"Invalid value for param {type.Name} - '{val}' - must be one of: `{string.Join("`, `", type.GetValues(session))}`");
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
                        vals[i] = GetBestInList(vals[i], possible);
                        if (vals[i] is null)
                        {
                            throw new InvalidDataException($"Invalid value for param {type.Name} - '{val}' - must be one of: `{string.Join("`, `", type.GetValues(session))}`");
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
                    throw new InvalidDataException($"Invalid image value for param {type.Name} - '{val}' - must be a valid base64 string - got '{shortText}'");
                }
                return val;
            case T2IParamDataType.IMAGE_LIST:
                List<string> parts = new();
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
                        throw new InvalidDataException($"Invalid image-list value for param {type.Name} - '{val}' - must be a valid base64 string - got '{shortText}'");
                    }
                    parts.Add(partVal);
                }
                return parts.JoinString("|");
            case T2IParamDataType.MODEL:
                if (!Program.T2IModelSets.TryGetValue(type.Subtype ?? "Stable-Diffusion", out T2IModelHandler handler))
                {
                    throw new InvalidDataException($"Invalid model sub-type for param {type.Name}: '{type.Subtype}' - are you sure that type name is correct? (Developer error)");
                }
                val = GetBestInList(val, handler.ListModelNamesFor(session).ToList());
                if (val is null)
                {
                    throw new InvalidDataException($"Invalid model value for param {type.Name} - '{val}' - are you sure that model name is correct?");
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
}
