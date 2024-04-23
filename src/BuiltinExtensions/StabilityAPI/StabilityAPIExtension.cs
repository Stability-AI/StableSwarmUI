
using FreneticUtilities.FreneticToolkit;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;

namespace StableSwarmUI.Builtin_StabilityAPIExtension;

public class StabilityAPIExtension : Extension
{
    public static List<string> Engines = ["stable-diffusion-v1-5"]; // Auto populated when it loads.

    public static LockObject TrackerLock = new();

    /// <summary>Set of all feature-ids supported by StabilityAPI backends.</summary>
    public static HashSet<string> FeaturesSupported = ["sapi"];

    public static T2IRegisteredParam<string> EngineParam;

    public override void OnInit()
    {
        T2IParamGroup sapiGroup = new("StabilityAPI", Toggles: false, Open: true);
        Program.Backends.RegisterBackendType<StabilityAPIBackend>("stability_api", "StabilityAPI", "A backend powered by the Stability API.", true);
        EngineParam = T2IParamTypes.Register<string>(new("[SAPI] Engine", "Engine for StabilityAPI to use.",
            "stable-diffusion-v1-5", Toggleable: true, FeatureFlag: "sapi", Group: sapiGroup, GetValues: (_) => Engines
            ));
    }
}
