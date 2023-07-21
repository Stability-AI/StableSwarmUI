
using Newtonsoft.Json.Linq;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.IO;

namespace StableSwarmUI.Builtin_ComfyUIBackend;

/// <summary>Helper class for generating ComfyUI workflows from input parameters.</summary>
public class WorkflowGenerator
{
    /// <summary>Represents a step in the workflow generation process.</summary>
    /// <param name="Action">The action to take.</param>
    /// <param name="Priority">The priority to apply it at.
    /// These are such from lowest to highest.
    /// "-10" is the priority of the first core pre-init,
    /// "0" is before final outputs,
    /// "10" is final output.</param>
    public record class WorkflowGenStep(Action<WorkflowGenerator> Action, double Priority);

    /// <summary>Callable steps for modifying workflows as they go.</summary>
    public static List<WorkflowGenStep> Steps = new();

    /// <summary>Register a new step to the workflow generator.</summary>
    public static void AddStep(Action<WorkflowGenerator> step, double priority)
    {
        Steps.Add(new(step, priority));
        Steps = Steps.OrderBy(s => s.Priority).ToList();
    }

    static WorkflowGenerator()
    {
        AddStep(g =>
        {
            g.CreateNode("CheckpointLoaderSimple", (_, n) =>
            {
                n["inputs"] = new JObject()
                {
                    ["ckpt_name"] = g.UserInput.Get(T2IParamTypes.Model).Name.Replace('/', Path.DirectorySeparatorChar)
                };
            }, "4");
        }, -10);
        AddStep(g =>
        {
            if (g.UserInput.TryGet(T2IParamTypes.InitImage, out Image img))
            {
                g.CreateNode("LoadImage", (_, n) =>
                {
                    n["inputs"] = new JObject()
                    {
                        ["image"] = "${init_image}"
                    };
                }, "15");
                g.CreateNode("VAEEncode", (_, n) =>
                {
                    n["inputs"] = new JObject()
                    {
                        ["pixels"] = new JArray() { "15", 0 },
                        ["vae"] = g.FinalVae
                    };
                }, "5");
            }
            else
            {
                g.CreateNode("EmptyLatentImage", (_, n) =>
                {
                    n["inputs"] = new JObject()
                    {
                        ["batch_size"] = "1",
                        ["height"] = g.UserInput.Get(T2IParamTypes.Height),
                        ["width"] = g.UserInput.Get(T2IParamTypes.Width)
                    };
                }, "5");
            }
        }, -9);
        AddStep(g =>
        {
            g.CreateNode("CLIPTextEncode", (_, n) =>
            {
                n["inputs"] = new JObject()
                {
                    ["clip"] = g.FinalClip,
                    ["text"] = g.UserInput.Get(T2IParamTypes.Prompt)
                };
            }, "6");
        }, -8);
        AddStep(g =>
        {
            g.CreateNode("CLIPTextEncode", (_, n) =>
            {
                n["inputs"] = new JObject()
                {
                    ["clip"] = g.FinalClip,
                    ["text"] = g.UserInput.Get(T2IParamTypes.NegativePrompt)
                };
            }, "7");
        }, -7);
        AddStep(g =>
        {
            g.CreateNode("KSamplerAdvanced", (_, n) =>
            {
                int startStep = 0;
                if (g.UserInput.TryGet(T2IParamTypes.InitImage, out Image _) && g.UserInput.TryGet(T2IParamTypes.InitImageCreativity, out double creativity))
                {
                    startStep = (int)(g.UserInput.Get(T2IParamTypes.Steps) * (1 - creativity));
                }
                n["inputs"] = new JObject()
                {
                    ["model"] = g.FinalModel,
                    ["add_noise"] = "enable",
                    ["noise_seed"] = g.UserInput.Get(T2IParamTypes.Seed),
                    ["steps"] = g.UserInput.Get(T2IParamTypes.Steps),
                    ["cfg"] = g.UserInput.Get(T2IParamTypes.CFGScale),
                    // TODO: proper sampler input, and intelligent default scheduler per sampler
                    ["sampler_name"] = g.UserInput.Get(ComfyUIBackendExtension.SamplerParam)?.ToString() ?? "euler",
                    ["scheduler"] = g.UserInput.Get(ComfyUIBackendExtension.SchedulerParam)?.ToString() ?? "normal",
                    ["positive"] = g.FinalPrompt,
                    ["negative"] = g.FinalNegativePrompt,
                    ["latent_image"] = g.FinalLatentImage,
                    // TODO: Configurable
                    ["start_at_step"] = startStep,
                    ["end_at_step"] = 10000,
                    ["return_with_leftover_noise"] = "disable"
                };
            }, "10");
        }, -1);
        AddStep(g =>
        {
            g.CreateNode("VAEDecode", (_, n) =>
            {
                n["inputs"] = new JObject()
                {
                    ["samples"] = g.FinalSamples,
                    ["vae"] = g.FinalVae
                };
            }, "8");
        }, 9);
        AddStep(g =>
        {
            g.CreateNode("SaveImage", (_, n) =>
            {
                n["inputs"] = new JObject()
                {
                    ["filename_prefix"] = $"StableSwarmUI_{Random.Shared.Next():X4}_",
                    ["images"] = g.FinalImageOut
                };
            }, "9");
        }, 10);
    }

    /// <summary>The raw user input data.</summary>
    public T2IParamInput UserInput;

    /// <summary>The output workflow object.</summary>
    public JObject Workflow;

    /// <summary>Lastmost node ID for key input trackers.</summary>
    public JArray FinalModel = new() { "4", 0 },
        FinalClip = new() { "4", 1 },
        FinalVae = new() { "4", 2 },
        FinalLatentImage = new() { "5", 0 },
        FinalPrompt = new() { "6", 0 },
        FinalNegativePrompt = new() { "7", 0 },
        FinalSamples = new() { "10", 0 },
        FinalImageOut = new() { "8", 0 };

    /// <summary>Mapping of any extra nodes to keep track of, Name->ID, eg "MyNode" -> "15".</summary>
    public Dictionary<string, string> NodeHelpers = new();

    /// <summary>Last used ID, tracked to safely add new nodes with sequential IDs. Note that this starts at 100, as below 100 is reserved for constant node IDs.</summary>
    public int LastID = 100;

    /// <summary>Creates a new node with the given class type and configuration action.</summary>
    public int CreateNode(string classType, Action<string, JObject> configure)
    {
        int id = LastID++;
        CreateNode(classType, configure, $"{id}");
        return id;
    }

    /// <summary>Creates a new node with the given class type and configuration action, and manual ID.</summary>
    public void CreateNode(string classType, Action<string, JObject> configure, string id)
    {
        JObject obj = new() { ["class_type"] = classType };
        configure(id, obj);
        Workflow[id] = obj;
    }

    /// <summary>Call to run the generation process and get the result.</summary>
    public JObject Generate()
    {
        Workflow = new();
        foreach (WorkflowGenStep step in Steps)
        {
            step.Action(this);
        }
        return Workflow;
    }
}
