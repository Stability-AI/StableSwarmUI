
using StableUI.Core;

namespace StableUI.Builtin_StabilityAPIExtension;

public class StabilityAPIExtension : Extension
{
    public override void OnInit()
    {
        Program.Backends.RegisterBackendType<StabilityAPIBackend>("stability_api", "StabilityAPI", "A backend powered by the Stability API.");
    }
}
