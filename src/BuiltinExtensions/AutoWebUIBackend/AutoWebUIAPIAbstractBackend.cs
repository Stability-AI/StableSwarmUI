using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Text2Image;
using StableUI.Utils;
using StableUI.Backends;
using System;
using System.Net.Http;

namespace StableUI.Builtin_AutoWebUIExtension;

/// <summary>T2I Backend using the Automatic1111/Stable-Diffusion-WebUI API.</summary>
public abstract class AutoWebUIAPIAbstractBackend : AbstractT2IBackend
{
    public abstract string Address { get; }

    /// <summary>Internal HTTP handler.</summary>
    public HttpClient HttpClient = NetworkBackendUtils.MakeHttpClient();

    public async Task InitInternal(bool ignoreWebError)
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            Status = BackendStatus.DISABLED;
            return;
        }
        try
        {
            string remoteModel = await QueryLoadedModel();
            if (remoteModel.EndsWith(']'))
            {
                remoteModel = remoteModel.BeforeLast(" [");
            }
            string targetClean = remoteModel.ToLowerInvariant().Trim('/').Replace('\\', '/');
            string targetBackup = targetClean.BeforeLast('.').AfterLast('/');
            foreach (T2IModel model in Program.T2IModels.Models.Values)
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

    public override Task Shutdown()
    {
        Status = BackendStatus.DISABLED;
        // Nothing to do, not our server.
        return Task.CompletedTask;
    }

    public override async Task<Image[]> Generate(T2IParamInput user_input)
    {
        JObject toSend = new()
        {
            ["prompt"] = user_input.Get(T2IParamTypes.Prompt),
            ["negative_prompt"] = user_input.Get(T2IParamTypes.NegativePrompt),
            ["seed"] = user_input.Get(T2IParamTypes.Seed),
            ["steps"] = user_input.Get(T2IParamTypes.Steps),
            ["width"] = user_input.Get(T2IParamTypes.Width),
            ["height"] = user_input.Get(T2IParamTypes.Height),
            ["cfg_scale"] = user_input.Get(T2IParamTypes.CFGScale),
            ["subseed"] = user_input.Get(T2IParamTypes.VariationSeed),
            ["subseed_strength"] = user_input.Get(T2IParamTypes.VariationSeedStrength)
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
        return result["images"].Select(i => new Image((string)i)).ToArray();
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
        return (string)((await SendGet<JObject>("options"))["sd_model_checkpoint"]);
    }

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

    public override IEnumerable<string> SupportedFeatures => AutoWebUIBackendExtension.FeaturesSupported;
}
