
using FreneticUtilities.FreneticToolkit;
using StableUI.Core;
using StableUI.Text2Image;

namespace StableUI.Builtin_StabilityAPIExtension;

public class StabilityAPIExtension : Extension
{
    public static List<string> Engines = new() { "stable-diffusion-v1-5" }; // Auto populated when it loads.

    public static LockObject TrackerLock = new();

    public static string[] Samplers = new[] { "DDIM", "DDPM", "K_DPMPP_2M", "K_DPMPP_2S_ANCESTRAL", "K_DPM_2", "K_DPM_2_ANCESTRAL", "K_EULER", "K_EULER_ANCESTRAL", "K_HEUN", "K_LMS" };

    /// <summary>Set of all feature-ids supported by StabilityAPI backends.</summary>
    public static HashSet<string> FeaturesSupported = new();

    public override void OnInit()
    {
        T2IParamGroup sapiGroup = new("StabilityAPI", Toggles: false, Open: true);
        Program.Backends.RegisterBackendType<StabilityAPIBackend>("stability_api", "StabilityAPI", "A backend powered by the Stability API.");
        T2IParamTypes.Register(new("[SAPI] Engine", "Engine for StabilityAPI to use.",
            T2IParamDataType.DROPDOWN, "stable-diffusion-v1-5", (s, p) => p.OtherParams["sapi_engine"] = s, Toggleable: true, FeatureFlag: "sapi", Group: sapiGroup,
            GetValues: (_) => Engines
            ));
        T2IParamTypes.Register(new("[SAPI] Sampler", "Sampler for StabilityAPI to use.",
            T2IParamDataType.DROPDOWN, "K_EULER", (s, p) => p.OtherParams["sapi_sampler"] = s, Toggleable: true, FeatureFlag: "sapi", Group: sapiGroup,
            GetValues: (_) => Samplers.ToList()
            ));
    }
}
