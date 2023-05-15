using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Text2Image;
using StableUI.Utils;
using System;

namespace StableUI.Backends;

/// <summary>T2I Backend using the Automatic1111/Stable-Diffusion-WebUI API.</summary>
public abstract class AutoWebUIAPIAbstractBackend<T> : AbstractT2IBackend<T> where T : AutoConfiguration
{
    public abstract string Address { get; }

    /// <summary>Internal HTTP handler.</summary>
    public HttpClient HttpClient = new();

    public AutoWebUIAPIAbstractBackend()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"StableUI/{Utilities.Version}");
    }

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

    public override async Task<Image[]> Generate(T2IParams user_input)
    {
        JObject result = await SendPost<JObject>("txt2img", new JObject()
        {
            ["prompt"] = user_input.Prompt,
            ["negative_prompt"] = user_input.NegativePrompt,
            ["seed"] = user_input.Seed,
            ["steps"] = user_input.Steps,
            ["width"] = user_input.Width,
            ["height"] = user_input.Height,
            ["cfg_scale"] = user_input.CFGScale
        });
        // TODO: Error handlers
        return result["images"].Select(i => new Image((string)i)).ToArray();
    }

    public static StringContent JSONContent(JObject jobj)
    {
        return new StringContent(jobj.ToString(Formatting.None), StringConversionHelper.UTF8Encoding, "application/json");
    }

    public static async Task<JType> Parse<JType>(HttpResponseMessage message) where JType : class
    {
        string content = await message.Content.ReadAsStringAsync();
        if (typeof(JType) == typeof(JObject)) // TODO: Surely C# has syntax for this?
        {
            return JObject.Parse(content) as JType;
        }
        else if (typeof(JType) == typeof(JArray))
        {
            return JArray.Parse(content) as JType;
        }
        else if (typeof(JType) == typeof(string))
        {
            return content as JType;
        }
        throw new NotImplementedException();
    }

    public async Task<JType> SendGet<JType>(string url) where JType : class
    {
        return await Parse<JType>(await HttpClient.GetAsync($"{Address}/sdapi/v1/{url}"));
    }

    public async Task<JType> SendPost<JType>(string url, JObject payload) where JType : class
    {
        return await Parse<JType>(await HttpClient.PostAsync($"{Address}/sdapi/v1/{url}", JSONContent(payload)));
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
}
