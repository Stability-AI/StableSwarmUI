
using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.DataHolders;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System;
using System.IO;
using System.Net.Http;
using System.Web;

namespace StableSwarmUI.Builtin_ComfyUIBackend;

public abstract class ComfyUIAPIAbstractBackend : AbstractT2IBackend
{
    public abstract string Address { get; }

    /// <summary>Internal HTTP handler.</summary>
    public HttpClient HttpClient = NetworkBackendUtils.MakeHttpClient();

    public JObject RawObjectInfo;

    public async Task InitInternal(bool ignoreWebError)
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            Status = BackendStatus.DISABLED;
            return;
        }
        try
        {
            JObject result = await SendGet<JObject>("object_info");
            if (result.TryGetValue("error", out JToken errorToken))
            {
                throw new Exception($"Remote error: {errorToken}");
            }
            RawObjectInfo = result;
            ComfyUIBackendExtension.AssignValuesFromRaw(RawObjectInfo);
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

    public async Task<JObject> PostJSONString(string route, string input, CancellationToken interrupt)
    {
        return await NetworkBackendUtils.Parse<JObject>(await HttpClient.PostAsync($"{Address}/{route}", new StringContent(input, StringConversionHelper.UTF8Encoding, "application/json"), interrupt));
    }

    public async Task<Image[]> AwaitJob(string workflow, CancellationToken interrupt)
    {
        workflow = $"{{\"prompt\": {workflow}}}";
        Logs.Verbose($"Will use workflow: {workflow}");
        JObject result = await PostJSONString("prompt", workflow, interrupt);
        Logs.Verbose($"ComfyUI prompt said: {result}");
        if (result.ContainsKey("error"))
        {
            Logs.Debug($"Error came from prompt: {workflow}");
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
            Thread.Sleep(50);
        }
        Logs.Verbose($"ComfyUI history said: {output}");
        List<Image> outputs = new();
        foreach (JToken outData in output[promptId]["outputs"].Values())
        {
            foreach (JToken outImage in outData["images"])
            {
                string fname = outImage["filename"].ToString();
                byte[] image = await (await HttpClient.GetAsync($"{Address}/view?filename={HttpUtility.UrlEncode(fname)}", interrupt)).Content.ReadAsByteArrayAsync(interrupt);
                if (image == null || image.Length == 0)
                {
                    Logs.Error($"Invalid/null/empty image data from ComfyUI server for '{fname}', under {outData}");
                    continue;
                }
                outputs.Add(new Image(image));
                PostResultCallback(fname);
            }
        }
        return outputs.ToArray();
    }

    public volatile int ImageIDDedup = 0;

    public override async Task<Image[]> Generate(T2IParamInput user_input)
    {
        string workflow = null;
        if (user_input.TryGetRaw(ComfyUIBackendExtension.FakeRawInputType, out object workflowRaw))
        {
            workflow = (string)workflowRaw;
            workflow = workflow.Replace("\"%%_COMFYFIXME_${", "${").Replace("}_ENDFIXME_%%\"", "}");
        }
        else if (user_input.TryGet(ComfyUIBackendExtension.WorkflowParam, out string workflowName))
        {
            if (!ComfyUIBackendExtension.Workflows.TryGetValue(workflowName, out workflow))
            {
                throw new InvalidDataException("Unrecognized ComfyUI Workflow name.");
            }
        }
        List<Action> completeSteps = new();
        string initImageFixer(string flow) // TODO: This is a hack.
        {
            if (workflow.Contains("${init_image}") && user_input.TryGet(T2IParamTypes.InitImage, out Image img))
            {
                int id = Interlocked.Increment(ref ImageIDDedup);
                string fname = $"init_image_sui_backend_{BackendData.ID}_{id}.png";
                Image fixedImage = img.Resize(user_input.Get(T2IParamTypes.Width), user_input.Get(T2IParamTypes.Height));
                MultipartFormDataContent content = new()
                    {
                        { new ByteArrayContent(fixedImage.ImageData), "image", fname },
                        { new StringContent("true"), "overwrite" }
                    };
                HttpClient.PostAsync($"{Address}/upload/image", content).Wait();
                completeSteps.Add(() =>
                {
                    Interlocked.Decrement(ref ImageIDDedup);
                });
                // TODO: Emit cleanup step to remove the image, or find a way to send it purely over network rather than needing file storage
                workflow = workflow.Replace("${init_image}", fname);
            }
            return workflow;
        }
        if (workflow is not null)
        {
            workflow = StringConversionHelper.QuickSimpleTagFiller(initImageFixer(workflow), "${", "}", (tag) => {
                string fixedTag = Utilities.UnescapeJsonString(tag);
                string tagName = fixedTag.BeforeAndAfter(':', out string defVal);
                string tagBasic = tagName.BeforeAndAfter('+', out string tagExtra);
                string filled = tagBasic switch
                {
                    "prompt" => user_input.Get(T2IParamTypes.Prompt),
                    "negative_prompt" => user_input.Get(T2IParamTypes.NegativePrompt),
                    "seed" => $"{user_input.Get(T2IParamTypes.Seed) + (int.TryParse(tagExtra, out int add) ? add : 0)}",
                    "steps" => $"{user_input.Get(T2IParamTypes.Steps)}",
                    "width" => $"{user_input.Get(T2IParamTypes.Width)}",
                    "height" => $"{user_input.Get(T2IParamTypes.Height)}",
                    "cfg_scale" => $"{user_input.Get(T2IParamTypes.CFGScale)}",
                    "subseed" => $"{user_input.Get(T2IParamTypes.VariationSeed)}",
                    "subseed_strength" => user_input.GetString(T2IParamTypes.VariationSeedStrength),
                    "init_image" => user_input.Get(T2IParamTypes.InitImage)?.AsBase64,
                    "init_image_strength" => user_input.GetString(T2IParamTypes.InitImageCreativity),
                    "comfy_sampler" => user_input.GetString(ComfyUIBackendExtension.SamplerParam) ?? "euler",
                    "comfy_scheduler" => user_input.GetString(ComfyUIBackendExtension.SchedulerParam) ?? "normal",
                    "model" => user_input.Get(T2IParamTypes.Model).ToString(),
                    "prefix" => $"StableSwarmUI_{Random.Shared.Next():X4}_",
                    _ => user_input.TryGetRaw(T2IParamTypes.GetType(tagBasic, user_input), out object val) ? val?.ToString() : null
                };
                if (tagExtra == "seed" && filled == "-1")
                {
                    filled = $"{Random.Shared.Next()}";
                }
                filled ??= defVal;
                return Utilities.EscapeJsonString(filled);
            });
        }
        else
        {
            workflow = new WorkflowGenerator() { UserInput = user_input }.Generate().ToString();
            workflow = initImageFixer(workflow);
        }
        try
        {
            return await AwaitJob(workflow, user_input.InterruptToken);
        }
        catch (Exception)
        {
            Logs.Debug($"Failed to process comfy workflow: {workflow} for inputs {user_input}");
            throw;
        }
        finally
        {
            foreach (Action step in completeSteps)
            {
                step();
            }
        }
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
        string workflow = ComfyUIBackendExtension.Workflows["just_load_model"].Replace("${model:error_missing_model}", Utilities.EscapeJsonString(model.ToString()));
        await AwaitJob(workflow, CancellationToken.None);
        CurrentModelName = model.Name;
        return true;
    }

    public override IEnumerable<string> SupportedFeatures => ComfyUIBackendExtension.FeaturesSupported;
}
