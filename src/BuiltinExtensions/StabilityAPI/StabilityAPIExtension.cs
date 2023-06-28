
using FreneticUtilities.FreneticToolkit;
using StableUI.Core;
using StableUI.Text2Image;

namespace StableUI.Builtin_StabilityAPIExtension;

public class StabilityAPIExtension : Extension
{
    public static List<string> Engines = new() { "stable-diffusion-v1-5" }; // Auto populated when it loads.

    public static LockObject TrackerLock = new();

    public override void OnInit()
    {
        Program.Backends.RegisterBackendType<StabilityAPIBackend>("stability_api", "StabilityAPI", "A backend powered by the Stability API.");
        T2IParamTypes.Register(new("[SAPI] Engine", "Engine for StabilityAPI to use.",
            T2IParamDataType.DROPDOWN, "stable-diffusion-v1-5", (s, p) => p.OtherParams["sapi_engine"] = s, Toggleable: true, FeatureFlag: "sapi", Group: "SAPI",
            GetValues: (_) => Engines
            ));
    }
}
