using Newtonsoft.Json.Linq;
using StableUI.Core;
using StableUI.DataHolders;

namespace StableUI.Builtin_AutoWebUIExtension;

/// <summary>Main class for the Automatic1111 Stable-Diffusion-WebUI Backend extension.</summary>
public class AutoWebUIBackendExtension : Extension
{

    /// <summary>List of actions to run when generating an image, primarily to alter the input data.</summary>
    public static List<Action<JObject, T2IParams>> OtherGenHandlers = new();

    /// <summary>Set of all feature-ids supported by Auto WebUI backends.</summary>
    public static HashSet<string> FeaturesSupported = new();

    public override void OnInit()
    {
        Program.Backends.RegisterBackendType<AutoWebUIAPIBackend>("auto_webui_api", "Auto1111 SD-WebUI API By URL", "A backend powered by a pre-existing installation of the AUTOMATIC1111/Stable-Diffusion-WebUI launched in '--api' mode, referenced via API base URL.");
        Program.Backends.RegisterBackendType<AutoWebUISelfStartBackend>("auto_webui_selfstart", "Auto111 SD-WebUI Self-Starting", "A backend powered by a pre-existing installation of the AUTOMATIC1111/Stable-Diffusion-WebUI, automatically launched and managed by this UI server.");
    }
}
