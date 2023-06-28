
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
using System.IO;
using System.Net.Http;
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

    public virtual void PostResultCallback(string filename)
    {
    }

    public async Task<Image[]> AwaitJob(string workflow, CancellationToken interrupt)
    {
        workflow = $"{{\"prompt\": {workflow}}}";
        Logs.Debug($"Will use workflow: {workflow}");
        JObject result = await NetworkBackendUtils.Parse<JObject>(await HttpClient.PostAsync($"{Address}/prompt", new StringContent(workflow, StringConversionHelper.UTF8Encoding, "application/json"), interrupt));
        Logs.Debug($"ComfyUI prompt said: {result}");
        if (result.ContainsKey("error"))
        {
            Logs.Error($"ComfyUI error: {result}");
            throw new Exception("ComfyUI errored");
        }
        string promptId = result["prompt_id"].ToString();
        JObject output;
        while (true)
        {
            output = await SendGet<JObject>($"history/{promptId}");
            if (!output.Properties().IsEmpty())
            {
                break;
            }
            if (Program.GlobalProgramCancel.IsCancellationRequested)
            {
                return null;
            }
            if (interrupt.IsCancellationRequested)
            {
                Logs.Debug("ComfyUI Interrupt requested");
                await HttpClient.PostAsync($"{Address}/interrupt", new StringContent(""), interrupt);
                break;
            }
            Thread.Sleep(100);
        }
        Logs.Debug($"ComfyUI history said: {output}");
        List<Image> outputs = new();
        foreach (JToken outData in output[promptId]["outputs"].Values())
        {
            foreach (JToken outImage in outData["images"])
            {
                string fname = outImage["filename"].ToString();
                byte[] image = await (await HttpClient.GetAsync($"{Address}/view?filename={HttpUtility.UrlEncode(fname)}", interrupt)).Content.ReadAsByteArrayAsync(interrupt);
                outputs.Add(new Image(image));
                PostResultCallback(fname);
            }
        }
        return outputs.ToArray();
    }

    public override async Task<Image[]> Generate(T2IParams user_input)
    {
        string workflow;
        if (user_input.OtherParams.TryGetValue("comfyui_workflow", out object workflowObj))
        {
            if (workflowObj is not string workflowName || !ComfyUIBackendExtension.Workflows.TryGetValue(workflowName, out workflow))
            {
                throw new InvalidDataException("Unrecognized ComfyUI Workflow name.");
            }
            workflow = StringConversionHelper.QuickSimpleTagFiller(workflow, "${", "}", (tag) => {
                string tagName = tag.BeforeAndAfter(':', out string defVal);
                string filled = tagName switch
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
                    "comfy_sampler" => user_input.OtherParams.GetValueOrDefault("comfyui_sampler")?.ToString(),
                    "comfy_scheduler" => user_input.OtherParams.GetValueOrDefault("comfyui_scheduler")?.ToString(),
                    "model" => user_input.Model.Name.Replace('/', Path.DirectorySeparatorChar),
                    "prefix" => $"StableUI_{Random.Shared.Next():X4}_",
                    _ => user_input.OtherParams.GetValueOrDefault(tagName)?.ToString()
                };
                filled ??= defVal;
                return Utilities.EscapeJsonString(filled);
            });
        }
        else
        {
            workflow = new WorkflowGenerator() { UserInput = user_input }.Generate().ToString();
        }
        return await AwaitJob(workflow, user_input.InterruptToken);
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
        string workflow = ComfyUIBackendExtension.Workflows["just_load_model"].Replace("${model:error_missing_model}", Utilities.EscapeJsonString(model.Name.Replace('/', Path.DirectorySeparatorChar)));
        await AwaitJob(workflow, CancellationToken.None);
        CurrentModelName = model.Name;
        return true;
    }

    public override bool DoesProvideFeature(string feature)
    {
        return ComfyUIBackendExtension.FeaturesSupported.Contains(feature);
    }
}
