using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Core;
using StableSwarmUI.DataHolders;
using StableSwarmUI.Text2Image;

namespace StableSwarmUI.Builtin_AutoWebUIExtension;

/// <summary>Main class for the Automatic1111 Stable-Diffusion-WebUI Backend extension.</summary>
public class AutoWebUIBackendExtension : Extension
{

    /// <summary>List of actions to run when generating an image, primarily to alter the input data.</summary>
    public static List<Action<JObject, T2IParamInput>> OtherGenHandlers = [];

    /// <summary>Set of all feature-ids supported by Auto WebUI backends.</summary>
    public static HashSet<string> FeaturesSupported = ["variation_seed", "autowebui"];

    public static T2IRegisteredParam<string> SamplerParam;

    public static List<string> Samplers = ["Euler a", "Euler"];

    public static LockObject ExtBackLock = new();

    public static void LoadSamplerList(List<string> newSamplers)
    {
        lock (ExtBackLock)
        {
            Samplers = Samplers.Union(newSamplers).Distinct().ToList();
        }
    }

    public override void OnInit()
    {
        T2IParamGroup autoWebuiGroup = new("Auto WebUI", Toggles: false, Open: false);
        SamplerParam = T2IParamTypes.Register<string>(new("[AutoWebUI] Sampler", "Sampler type (for AutoWebUI)",
            "Euler", Toggleable: true, FeatureFlag: "autowebui", Group: autoWebuiGroup,
            GetValues: (_) => Samplers
            ));
        Program.Backends.RegisterBackendType<AutoWebUIAPIBackend>("auto_webui_api", "Auto1111 SD-WebUI API By URL",
            "A backend powered by a pre-existing installation of the AUTOMATIC1111/Stable-Diffusion-WebUI launched in '--api' mode, referenced via API base URL.", true);
        Program.Backends.RegisterBackendType<AutoWebUISelfStartBackend>("auto_webui_selfstart", "Auto111 SD-WebUI Self-Starting",
            "A backend powered by a pre-existing installation of the AUTOMATIC1111/Stable-Diffusion-WebUI, automatically launched and managed by this UI server.");
    }
}
