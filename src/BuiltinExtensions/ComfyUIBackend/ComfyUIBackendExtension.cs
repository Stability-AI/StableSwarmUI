using FreneticUtilities.FreneticExtensions;
using StableUI.Builtin_AutoWebUIExtension;
using StableUI.Core;
using StableUI.Text2Image;
using System.IO;

namespace StableUI.Builtin_ComfyUIBackend;

/// <summary>Main class for the ComfyUI Backend extension.</summary>
public class ComfyUIBackendExtension : Extension
{
    public static string Folder;

    public static Dictionary<string, string> Workflows;

    /// <summary>Set of all feature-ids supported by ComfyUI backends.</summary>
    public static HashSet<string> FeaturesSupported = new() { "comfyui" };

    public override void OnPreInit()
    {
        Folder = FilePath;
        Workflows = new();
        foreach (string workflow in Directory.EnumerateFiles($"{Folder}/Workflows"))
        {
            Workflows.Add(workflow.Replace('\\', '/').AfterLast('/').BeforeLast('.'), File.ReadAllText(workflow));
        }
    }

    public override void OnInit()
    {
        T2IParamTypes.Register(new("[ComfyUI] Workflow", "What workflow to use in ComfyUI (files in 'Workflows' folder within the ComfyUI extension)",
            T2IParamDataType.DROPDOWN, "basic", (s, p) => p.OtherParams["comfyui_workflow"] = s, Toggleable: true, FeatureFlag: "comfyui", Group: "ComfyUI",
            GetValues: (_) => Workflows.Keys.ToList()
            ));
        T2IParamTypes.Register(new("[ComfyUI] Sampler", "Sampler type (for ComfyUI)",
            T2IParamDataType.DROPDOWN, "euler", (s, p) => p.OtherParams["comfyui_sampler"] = s, Toggleable: true, FeatureFlag: "comfyui", Group: "ComfyUI",
            GetValues: (_) => new() { "euler", "euler_ancestral", "heun", "dpm_2", "dpm_2_ancestral", "lms", "dpm_fast", "dpm_adaptive", "dpmpp_2s_ancestral", "dpmpp_sde", "dpmpp_2m", "dpmpp_2m_sde", "ddim", "uni_pc", "uni_pc_bh2" }
            ));
        T2IParamTypes.Register(new("[ComfyUI] Scheduler", "Scheduler type (for ComfyUI)",
            T2IParamDataType.DROPDOWN, "normal", (s, p) => p.OtherParams["comfyui_scheduler"] = s, Toggleable: true, FeatureFlag: "comfyui", Group: "ComfyUI",
            GetValues: (_) => new() { "normal", "karras", "exponential", "simple", "ddim_uniform" }
            ));
        Program.Backends.RegisterBackendType<ComfyUIAPIBackend>("comfyui_api", "ComfyUI API By URL", "A backend powered by a pre-existing installation of ComfyUI, referenced via API base URL.");
        Program.Backends.RegisterBackendType<ComfyUISelfStartBackend>("comfyui_selfstart", "ComfyUI Self-Starting", "A backend powered by a pre-existing installation of the ComfyUI, automatically launched and managed by this UI server.");
    }
}
