
using StableUI.Builtin_ComfyUIBackend;
using StableUI.Core;
using StableUI.Text2Image;

namespace StableUI.Builtin_DynamicThresholding;

public class DynamicThresholdingExtension : Extension
{
    public static T2IRegisteredParam<double> MimicScale, ThresholdPercentile, CFGScaleMin, MimicScaleMin, ExperimentMode, SchedulerValue;

    public static T2IRegisteredParam<string> CFGScaleMode, MimicScaleMode;

    public override void OnInit()
    {
        T2IParamGroup dynThreshGroup = new("Dynamic Thresholding", Toggles: true, Open: false, IsAdvanced: true);
        MimicScale = T2IParamTypes.Register<double>(new("[DT] Mimic Scale", "[Dynamic Thresholding]\nMimic Scale value (target for the CFG Scale recentering).",
            "7", Min: 0, Max: 100, Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding",
            Examples: new[] { "5", "7", "9" }
            ));
        ThresholdPercentile = T2IParamTypes.Register<double>(new("[DT] Threshold Percentile", "[Dynamic Thresholding]\nthresholding percentile. '1' disables, '0.95' is decent value for enabled.",
            "1", Min: 0, Max: 1, Step: 0.05, Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding",
            Examples: new[] { "1", "0.99", "0.95", "0.9" }
            ));
        CFGScaleMode = T2IParamTypes.Register<string>(new("[DT] CFG Scale Mode", "[Dynamic Thresholding]\nMode for the CFG Scale scheduler.",
            "Constant", Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding",
            GetValues: (_) => new() { "Constant", "Linear Down", "Half Cosine Down", "Cosine Down", "Linear Up", "Half Cosine Up", "Cosine Up", "Power Up", "Power Down", "Linear Repeating", "Cosine Repeating" }
            ));
        CFGScaleMin = T2IParamTypes.Register<double>(new("[DT] CFG Scale Minimum", "[Dynamic Thresholding]\nCFG Scale minimum value (for non-constant CFG mode).",
            "0", Min: 0, Max: 100, Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding",
            Examples: new[] { "0", "1", "2", "5" }
            ));
        MimicScaleMode = T2IParamTypes.Register<string>(new("[DT] Mimic Scale Mode", "[Dynamic Thresholding]\nMode for the Mimic Scale scheduler.",
            "Constant", Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding",
            GetValues: (_) => new() { "Constant", "Linear Down", "Half Cosine Down", "Cosine Down", "Linear Up", "Half Cosine Up", "Cosine Up", "Power Up", "Power Down", "Linear Repeating", "Cosine Repeating" }
            ));
        MimicScaleMin = T2IParamTypes.Register<double>(new("[DT] Mimic Scale Minimum", "[Dynamic Thresholding]\nMimic Scale minimum value (for non-constant mimic mode).",
            "0", Min: 0, Max: 100, Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding",
            Examples: new[] { "0", "1", "2", "5" }
            ));
        SchedulerValue = T2IParamTypes.Register<double>(new("[DT] Scheduler Value", "[Dynamic Thresholding]\nIf either scale scheduler is 'Power', this is the power factor. If using 'repeating', this is the number of repeats per image. Otherwise, it does nothing.",
            "4", Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding",
            Examples: new[] { "2", "4", "8" }
            ));
        ExperimentMode = T2IParamTypes.Register<double>(new("[DT] Experiment Mode", "[Dynamic Thresholding]\nInternal experiment mode flag. '0' disables.",
            "0", Toggleable: true, Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding"
            ));
        // TODO: Auto WebUI Converter (use DynThres ext on auto webui)
        // TODO: ComfyUI support? Need a node for it
    }
}
