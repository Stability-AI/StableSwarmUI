
using FreneticUtilities.FreneticToolkit;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;

namespace StableSwarmUI.Builtin_StabilityAPIExtension;

public class StabilityAPIExtension : Extension
{
    public static List<string> Engines = ["sd3", "sd3-turbo"];

    public static LockObject TrackerLock = new();

    /// <summary>Set of all feature-ids supported by StabilityAPI backends.</summary>
    public static HashSet<string> FeaturesSupported = ["sapi"];

    public static T2IRegisteredParam<string> EngineParam;

    public override void OnInit()
    {
        T2IParamGroup sapiGroup = new("StabilityAPI", Toggles: false, Open: true);
        Program.Backends.RegisterBackendType<StabilityAPIBackend>("stability_api", "StabilityAPI", "A backend powered by the Stability API.", true);
        EngineParam = T2IParamTypes.Register<string>(new("[SAPI] Model", "Model for StabilityAPI to use.",
            "sd3", Toggleable: false, FeatureFlag: "sapi", Group: sapiGroup, GetValues: (_) => Engines
            ));
    }
}
