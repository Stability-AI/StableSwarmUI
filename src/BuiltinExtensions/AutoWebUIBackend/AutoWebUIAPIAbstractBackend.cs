using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Core;
using StableSwarmUI.DataHolders;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using StableSwarmUI.Backends;
using System;
using System.Net.Http;

namespace StableSwarmUI.Builtin_AutoWebUIExtension;

/// <summary>T2I Backend using the Automatic1111/Stable-Diffusion-WebUI API.</summary>
public abstract class AutoWebUIAPIAbstractBackend : AbstractT2IBackend
{
    public abstract string Address { get; }

    /// <summary>Internal HTTP handler.</summary>
    public static HttpClient HttpClient = NetworkBackendUtils.MakeHttpClient();

    public async Task InitInternal(bool ignoreWebError)
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            Status = BackendStatus.DISABLED;
            return;
        }
        try
        {
            string remoteModel = await QueryLoadedModel() ?? "";
            if (remoteModel.EndsWith(']'))
            {
                remoteModel = remoteModel.BeforeLast(" [");
            }
            string targetClean = remoteModel.ToLowerInvariant().Trim('/').Replace('\\', '/');
            string targetBackup = targetClean.BeforeLast('.').AfterLast('/');
            foreach (T2IModel model in Program.MainSDModels.Models.Values)
            {
                string cleaned = model.Name.ToLowerInvariant();
                if (cleaned == targetClean)
                {
                    CurrentModelName = model.Name;
                    break;
                }
                if (cleaned.BeforeLast('.').AfterLast('/') == targetBackup)
                {
                    CurrentModelName = model.Name;
                }
            }
            List<string> samplers = (await SendGet<JArray>("samplers")).Select(obj => (string)obj["name"]).ToList();
            AutoWebUIBackendExtension.LoadSamplerList(samplers);
            Status = BackendStatus.RUNNING;
        }
        catch (Exception)
        {
            if (!ignoreWebError)
            {
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public override Task Shutdown()
    {
        Status = BackendStatus.DISABLED;
        // Nothing to do, not our server.
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task<Image[]> Generate(T2IParamInput user_input)
    {
        user_input.ProcessPromptEmbeds(x => x.BeforeLast('.'));
        string promptAdd = "";
        if (user_input.TryGet(T2IParamTypes.Loras, out List<string> loras) && user_input.TryGet(T2IParamTypes.LoraWeights, out List<string> loraWeights) && loras.Count > 0 && loras.Count == loraWeights.Count)
        {
            for (int i = 0; i < loras.Count; i++)
            {
                string lora = loras[i];
                if (lora.EndsWith(".safetensors"))
                {
                    lora = lora.BeforeLast('.');
                }
                promptAdd += $"<lora:{lora}:{loraWeights[i]}>";
            }
        }
        JObject toSend = new()
        {
            ["prompt"] = user_input.Get(T2IParamTypes.Prompt) + promptAdd,
            ["negative_prompt"] = user_input.Get(T2IParamTypes.NegativePrompt),
            ["seed"] = user_input.Get(T2IParamTypes.Seed),
            ["steps"] = user_input.Get(T2IParamTypes.Steps),
            ["width"] = user_input.Get(T2IParamTypes.Width),
            ["height"] = user_input.GetImageHeight(),
            ["batch_size"] = user_input.Get(T2IParamTypes.BatchSize, 1),
            ["cfg_scale"] = user_input.Get(T2IParamTypes.CFGScale),
            ["subseed"] = user_input.Get(T2IParamTypes.VariationSeed),
            ["subseed_strength"] = user_input.Get(T2IParamTypes.VariationSeedStrength),
            ["sampler_name"] = user_input.Get(AutoWebUIBackendExtension.SamplerParam) ?? "Euler"
        };
        string route = "txt2img";
        if (user_input.TryGet(T2IParamTypes.InitImage, out Image initImg))
        {
            route = "img2img";
            toSend["init_images"] = new JArray(initImg.AsBase64);
            toSend["denoising_strength"] = user_input.Get(T2IParamTypes.InitImageCreativity);
        }
        foreach (Action<JObject, T2IParamInput> handler in AutoWebUIBackendExtension.OtherGenHandlers)
        {
            handler(toSend, user_input);
        }
        JObject result = await SendPost<JObject>(route, toSend);
        // TODO: Error handlers
        return result["images"].Select(i => new Image((string)i, Image.ImageType.IMAGE, "png")).ToArray();
    }

    public async Task<JType> SendGet<JType>(string url) where JType : class
    {
        return await NetworkBackendUtils.Parse<JType>(await HttpClient.GetAsync($"{Address}/sdapi/v1/{url}"));
    }

    public async Task<JType> SendPost<JType>(string url, JObject payload) where JType : class
    {
        return await NetworkBackendUtils.Parse<JType>(await HttpClient.PostAsync($"{Address}/sdapi/v1/{url}", Utilities.JSONContent(payload)));
    }

    public async Task<string> QueryLoadedModel()
    {
        return (string)(await SendGet<JObject>("options"))["sd_model_checkpoint"];
    }

    /// <inheritdoc/>
    public override async Task<bool> LoadModel(T2IModel model)
    {
        string targetClean = model.Name.ToLowerInvariant().Trim('/');
        string targetBackup = targetClean.BeforeLast('.').AfterLast('/');
        string name = null;
        JArray models = await SendGet<JArray>("sd-models");
        foreach (JObject modelObj in models.Cast<JObject>())
        {
            string title = ((string)modelObj["title"]);
            if (title.EndsWith(']'))
            {
                title = title.BeforeLast(" [");
            }
            string cleaned = title.ToLowerInvariant().Replace('\\', '/').Trim('/');
            if (cleaned == targetClean)
            {
                name = title;
                break;
            }
            if (cleaned.BeforeLast('.').AfterLast('/') == targetBackup)
            {
                name = title;
            }
        }
        if (name is null)
        {
            return false;
        }
        CurrentModelName = model.Name;
        await SendPost<string>("options", new JObject() { ["sd_model_checkpoint"] = name });
        return true;
    }

    /// <inheritdoc/>
    public override IEnumerable<string> SupportedFeatures => AutoWebUIBackendExtension.FeaturesSupported;
}
