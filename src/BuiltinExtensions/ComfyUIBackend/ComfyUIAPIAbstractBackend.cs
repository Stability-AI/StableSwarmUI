
using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableUI.Backends;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Text2Image;
using StableUI.Utils;
using System;
using System.Web;

namespace StableUI.Builtin_ComfyUIBackend;

public abstract class ComfyUIAPIAbstractBackend<T> : AbstractT2IBackend<T> where T : AutoConfiguration
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
            JObject result = await SendGet<JObject>("system_stats");
            if (result.TryGetValue("error", out JToken errorToken))
            {
                throw new Exception($"Remote error: {errorToken}");
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
        if (!user_input.OtherParams.TryGetValue("comfyui_workflow", out object workflowObj) || workflowObj is not string workflowName)
        {
            throw new InvalidDataException("Must select a ComfyUI Workflow.");
        }
        if (!ComfyUIBackendExtension.Workflows.TryGetValue(workflowName, out string workflow))
        {
            throw new InvalidDataException("Unrecognized ComfyUI Workflow name.");
        }
        workflow = StringConversionHelper.QuickSimpleTagFiller(workflow, "${", "}", (tag) => Utilities.EscapeJsonString(tag switch
        {
            "prompt" => user_input.Prompt,
            "negative_prompt" => user_input.NegativePrompt,
            "seed" => $"{user_input.Seed}",
            "steps" => $"{user_input.Steps}",
            "width" => $"{user_input.Width}",
            "height" => $"{user_input.Height}",
            "cfg_scale" => $"{user_input.CFGScale}",
            "subseed" => $"{user_input.VarSeed}",
            "subseed_strength" => $"{user_input.VarSeedStrength}",
            "init_image" => user_input.InitImage.AsBase64,
            "init_image_strength" => $"{user_input.ImageInitStrength}",
            "comfy_sampler" => user_input.OtherParams.GetValueOrDefault("comfy_sampler")?.ToString() ?? "euler",
            "comfy_scheduler" => user_input.OtherParams.GetValueOrDefault("comfy_scheduler")?.ToString() ?? "normal",
            "model" => user_input.Model.Name.Replace('/', Path.DirectorySeparatorChar),
            _ => user_input.OtherParams.GetValueOrDefault(tag)?.ToString() ?? tag
        }));
        workflow = $"{{\"prompt\": {workflow}}}";
        JObject result = await NetworkBackendUtils.Parse<JObject>(await HttpClient.PostAsync($"{Address}/prompt", new StringContent(workflow, StringConversionHelper.UTF8Encoding, "application/json")));
        Logs.Debug($"ComfyUI prompt said: {result}");
        string promptId = result["prompt_id"].ToString();
        JObject output;
        while (true)
        {
            output = await SendGet<JObject>($"history/{promptId}");
            if (!output.Properties().IsEmpty())
            {
                break;
            }
            Thread.Sleep(100);
        }
        Logs.Debug($"ComfyUI history said: {output}");
        List<Image> outputs = new();
        foreach (JObject outData in output[promptId]["outputs"].Values())
        {
            foreach (JObject outImage in outData["images"])
            {
                Logs.Info($"Got output: {outImage}");
                string fname = outImage["filename"].ToString();
                byte[] image = await (await HttpClient.GetAsync($"{Address}/view?filename={HttpUtility.UrlEncode(fname)}")).Content.ReadAsByteArrayAsync();
                outputs.Add(new Image(image));
            }
        }
        return outputs.ToArray();
    }

    public async Task<JType> SendGet<JType>(string url) where JType : class
    {
        return await NetworkBackendUtils.Parse<JType>(await HttpClient.GetAsync($"{Address}/{url}"));
    }

    public async Task<JType> SendPost<JType>(string url, JObject payload) where JType : class
    {
        return await NetworkBackendUtils.Parse<JType>(await HttpClient.PostAsync($"{Address}/{url}", Utilities.JSONContent(payload)));
    }

    public override async Task<bool> LoadModel(T2IModel model)
    {
        // TODO: ComfyUI doesn't preload models, so... no reasonable implementation here.
        CurrentModelName = model.Name;
        return true;
    }

    public override bool DoesProvideFeature(string feature)
    {
        if (feature == "comfyui")
        {
            return true;
        }
        return false;
    }
}
