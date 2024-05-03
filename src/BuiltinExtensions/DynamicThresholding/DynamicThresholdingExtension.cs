
using Newtonsoft.Json.Linq;
using StableSwarmUI.Builtin_ComfyUIBackend;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;

namespace StableSwarmUI.Builtin_DynamicThresholding;

public class DynamicThresholdingExtension : Extension
{
    public static T2IRegisteredParam<double> MimicScale, ThresholdPercentile, CFGScaleMin, MimicScaleMin, SchedulerValue, InterpolatePhi;

    public static T2IRegisteredParam<string> CFGScaleMode, MimicScaleMode, ScalingStartpoint, VariabilityMeasure;

    public static T2IRegisteredParam<bool> SeparateFeatureChannels;

    public override void OnInit()
    {
        T2IParamGroup dynThreshGroup = new("Dynamic Thresholding", Toggles: true, Open: false, IsAdvanced: true);
        MimicScale = T2IParamTypes.Register<double>(new("[DT] Mimic Scale", "[Dynamic Thresholding]\nMimic Scale value (target for the CFG Scale recentering).",
            "7", Min: 0, Max: 100, Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding", OrderPriority: 1,
            Examples: ["5", "7", "9"]
            ));
        ThresholdPercentile = T2IParamTypes.Register<double>(new("[DT] Threshold Percentile", "[Dynamic Thresholding]\nthresholding percentile. '1' disables, '0.95' is decent value for enabled.",
            "1", Min: 0, Max: 1, Step: 0.05, Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding", OrderPriority: 2,
            Examples: ["1", "0.99", "0.95", "0.9"]
            ));
        CFGScaleMode = T2IParamTypes.Register<string>(new("[DT] CFG Scale Mode", "[Dynamic Thresholding]\nMode for the CFG Scale scheduler.",
            "Constant", Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding", OrderPriority: 3,
            GetValues: (_) => ["Constant", "Linear Down", "Half Cosine Down", "Cosine Down", "Linear Up", "Half Cosine Up", "Cosine Up", "Power Up", "Power Down", "Linear Repeating", "Cosine Repeating"]
            ));
        CFGScaleMin = T2IParamTypes.Register<double>(new("[DT] CFG Scale Minimum", "[Dynamic Thresholding]\nCFG Scale minimum value (for non-constant CFG mode).",
            "0", Min: 0, Max: 100, Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding", OrderPriority: 4,
            Examples: ["0", "1", "2", "5"]
            ));
        MimicScaleMode = T2IParamTypes.Register<string>(new("[DT] Mimic Scale Mode", "[Dynamic Thresholding]\nMode for the Mimic Scale scheduler.",
            "Constant", Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding", OrderPriority: 5,
            GetValues: (_) => ["Constant", "Linear Down", "Half Cosine Down", "Cosine Down", "Linear Up", "Half Cosine Up", "Cosine Up", "Power Up", "Power Down", "Linear Repeating", "Cosine Repeating"]
            ));
        MimicScaleMin = T2IParamTypes.Register<double>(new("[DT] Mimic Scale Minimum", "[Dynamic Thresholding]\nMimic Scale minimum value (for non-constant mimic mode).",
            "0", Min: 0, Max: 100, Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding", OrderPriority: 6,
            Examples: ["0", "1", "2", "5"]
            ));
        SchedulerValue = T2IParamTypes.Register<double>(new("[DT] Scheduler Value", "[Dynamic Thresholding]\nIf either scale scheduler is 'Power', this is the power factor.\nIf using 'repeating', this is the number of repeats per image. Otherwise, it does nothing.",
            "4", Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding", OrderPriority: 7,
            Examples: ["2", "4", "8"]
            ));
        SeparateFeatureChannels = T2IParamTypes.Register<bool>(new("[DT] Separate Feature Channels", "[Dynamic Thresholding]\nWhether to separate the feature channels.\nNormally leave this on. I think it should be off for RCFG?",
            "true", Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding", OrderPriority: 8
            ));
        ScalingStartpoint = T2IParamTypes.Register<string>(new("[DT] Scaling Startpoint", "[Dynamic Thresholding]\nWhether to scale relative to the mean value or to zero.\nUse 'MEAN' normally. If you want RCFG logic, use 'ZERO'.",
            "MEAN", Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding", OrderPriority: 9, GetValues: (_) => ["MEAN", "ZERO"]
            ));
        VariabilityMeasure = T2IParamTypes.Register<string>(new("[DT] Variability Measure", "[Dynamic Thresholding]\nWhether to use standard deviation ('STD') or thresholded absolute values ('AD').\nNormally use 'AD'. Use 'STD' if wanting RCFG logic.",
            "AD", Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding", OrderPriority: 10, GetValues: (_) => ["AD", "STD"]
            ));
        InterpolatePhi = T2IParamTypes.Register<double>(new("[DT] Interpolate Phi", "[Dynamic Thresholding]\n'phi' interpolation factor.\nInterpolates between original value and DT value, such that 0.0 = use original, and 1.0 = use DT.\n(This exists because RCFG is bad and so half-removing it un-breaks it - better to just not do RCFG).",
            "1", Min: 0, Max: 1, Step: 0.05, Group: dynThreshGroup, FeatureFlag: "dynamic_thresholding", OrderPriority: 11,
            Examples: ["0", "0.25", "0.5", "0.75", "1"]
            ));

        // TODO: Auto WebUI Converter (use DynThres ext on auto webui)
        ComfyUIBackendExtension.NodeToFeatureMap["DynamicThresholdingFull"] = "dynamic_thresholding";
        WorkflowGenerator.AddStep(g =>
        {
            if (ComfyUIBackendExtension.FeaturesSupported.Contains("dynamic_thresholding") && g.UserInput.TryGet(MimicScale, out double mimicScale))
            {
                string newNode = g.CreateNode("DynamicThresholdingFull", new JObject()
                {
                    ["model"] = g.FinalModel,
                    ["mimic_scale"] = mimicScale,
                    ["threshold_percentile"] = g.UserInput.Get(ThresholdPercentile),
                    ["mimic_mode"] = g.UserInput.Get(MimicScaleMode),
                    ["mimic_scale_min"] = g.UserInput.Get(MimicScaleMin),
                    ["cfg_mode"] = g.UserInput.Get(CFGScaleMode),
                    ["cfg_scale_min"] = g.UserInput.Get(CFGScaleMin),
                    ["sched_val"] = g.UserInput.Get(SchedulerValue),
                    ["separate_feature_channels"] = g.UserInput.Get(SeparateFeatureChannels) ? "enable" : "disable",
                    ["scaling_startpoint"] = g.UserInput.Get(ScalingStartpoint),
                    ["variability_measure"] = g.UserInput.Get(VariabilityMeasure),
                    ["interpolate_phi"] = g.UserInput.Get(InterpolatePhi)
                });
                g.FinalModel = [$"{newNode}", 0];
            }
        }, -5.5);
    }
}
