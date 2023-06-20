
using StableUI.Builtin_ComfyUIBackend;
using StableUI.Core;
using StableUI.Text2Image;

namespace StableUI.Builtin_DynamicThresholding;

public class DynamicThresholdingExtension : Extension
{
    public override void OnInit()
    {
        T2IParamTypes.Register(new("[DT] Mimic Scale", "[Dynamic Thresholding]\nMimic Scale value (target for the CFG Scale recentering).",
            T2IParamDataType.DECIMAL, "7", (s, p) => p.OtherParams["dt_mimic_scale"] = s, Min: 0, Max: 100, Toggleable: true, IsAdvanced: true, Group: "Dynamic Thresholding", FeatureFlag: "dynamic_thresholding",
            Examples: new[] { "5", "7", "9" }
            ));
        T2IParamTypes.Register(new("[DT] Threshold Percentile", "[Dynamic Thresholding]\nthresholding percentile. '1' disables, '0.95' is decent value for enabled.",
            T2IParamDataType.DECIMAL, "1", (s, p) => p.OtherParams["dt_threshold_percentile"] = s, Min: 0, Max: 1, Step: 0.05, Toggleable: true, IsAdvanced: true, Group: "Dynamic Thresholding", FeatureFlag: "dynamic_thresholding",
            Examples: new[] { "1", "0.99", "0.95", "0.9" }
            ));
        T2IParamTypes.Register(new("[DT] CFG Scale Mode", "[Dynamic Thresholding]\nMode for the CFG Scale scheduler.",
            T2IParamDataType.DROPDOWN, "Constant", (s, p) => p.OtherParams["dt_cfg_mode"] = s, Toggleable: true, IsAdvanced: true, Group: "Dynamic Thresholding", FeatureFlag: "dynamic_thresholding",
            GetValues: (_) => new() { "Constant", "Linear Down", "Half Cosine Down", "Cosine Down", "Linear Up", "Half Cosine Up", "Cosine Up", "Power Up", "Power Down", "Linear Repeating", "Cosine Repeating" }
            ));
        T2IParamTypes.Register(new("[DT] CFG Scale Minimum", "[Dynamic Thresholding]\nCFG Scale minimum value (for non-constant CFG mode).",
            T2IParamDataType.DECIMAL, "0", (s, p) => p.OtherParams["dt_cfg_scale_min"] = s, Min: 0, Max: 100, Toggleable: true, IsAdvanced: true, Group: "Dynamic Thresholding", FeatureFlag: "dynamic_thresholding",
            Examples: new[] { "0", "1", "2", "5" }
            ));
        T2IParamTypes.Register(new("[DT] Mimic Scale Mode", "[Dynamic Thresholding]\nMode for the Mimic Scale scheduler.",
            T2IParamDataType.DROPDOWN, "Constant", (s, p) => p.OtherParams["dt_mimic_mode"] = s, Toggleable: true, IsAdvanced: true, Group: "Dynamic Thresholding", FeatureFlag: "dynamic_thresholding",
            GetValues: (_) => new() { "Constant", "Linear Down", "Half Cosine Down", "Cosine Down", "Linear Up", "Half Cosine Up", "Cosine Up", "Power Up", "Power Down", "Linear Repeating", "Cosine Repeating" }
            ));
        T2IParamTypes.Register(new("[DT] Mimic Scale Minimum", "[Dynamic Thresholding]\nMimic Scale minimum value (for non-constant mimic mode).",
            T2IParamDataType.DECIMAL, "0", (s, p) => p.OtherParams["dt_mimic_scale_min"] = s, Min: 0, Max: 100, Toggleable: true, IsAdvanced: true, Group: "Dynamic Thresholding", FeatureFlag: "dynamic_thresholding",
            Examples: new[] { "0", "1", "2", "5" }
            ));
        T2IParamTypes.Register(new("[DT] Scheduler Value", "[Dynamic Thresholding]\nIf either scale scheduler is 'Power', this is the power factor. If using 'repeating', this is the number of repeats per image.",
            T2IParamDataType.DECIMAL, "4", (s, p) => p.OtherParams["dt_power_val"] = s, Toggleable: true, IsAdvanced: true, Group: "Dynamic Thresholding", FeatureFlag: "dynamic_thresholding",
            Examples: new[] { "2", "4", "8" }
            ));
        T2IParamTypes.Register(new("[DT] Experiment Mode", "[Dynamic Thresholding]\nInternal experiment mode flag. '0' to disable.",
            T2IParamDataType.DECIMAL, "0", (s, p) => p.OtherParams["dt_experiment_mode"] = s, Toggleable: true, IsAdvanced: true, Group: "Dynamic Thresholding", FeatureFlag: "dynamic_thresholding"
            ));
        ComfyUIBackendExtension.FeaturesSupported.Add("dynamic_thresholding");
        // TODO: Auto WebUI Converter (use DynThres ext on auto webui)
    }
}
