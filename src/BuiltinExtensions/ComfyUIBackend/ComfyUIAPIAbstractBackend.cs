
using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Web;
using Newtonsoft.Json;
using System.Xml.Linq;

namespace StableSwarmUI.Builtin_ComfyUIBackend;

public abstract class ComfyUIAPIAbstractBackend : AbstractT2IBackend
{
    public abstract string Address { get; }

    /// <summary>Internal HTTP handler.</summary>
    public HttpClient HttpClient = NetworkBackendUtils.MakeHttpClient();

    public JObject RawObjectInfo;

    public async Task LoadValueSet()
    {
        JObject result = await SendGet<JObject>("object_info");
        if (result.TryGetValue("error", out JToken errorToken))
        {
            throw new Exception($"Remote error: {errorToken}");
        }
        RawObjectInfo = result;
        ComfyUIBackendExtension.AssignValuesFromRaw(RawObjectInfo);
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
            await LoadValueSet();
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

    /// <summary>Connects a client websocket to the backend.</summary>
    /// <param name="path">The path to connect on, after the '/', such as 'ws?clientId={uuid}'.</param>
    public async Task<ClientWebSocket> ConnectWebsocket(string path)
    {
        ClientWebSocket outSocket = new();
        outSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        string scheme = Address.BeforeAndAfter("://", out string addr);
        scheme = scheme == "http" ? "ws" : "wss";
        await outSocket.ConnectAsync(new Uri($"{scheme}://{addr}/{path}"), Program.GlobalProgramCancel);
        return outSocket;
    }

    /// <summary>Runs a job with live feedback (progress updates, previews, etc.)</summary>
    /// <param name="workflow">The workflow JSON to use.</param>
    /// <param name="batchId">Local batch-ID for this generation.</param>
    /// <param name="takeOutput">Takes an output object: Image for final images, JObject for anything else.</param>
    /// <param name="interrupt">Interrupt token to use.</param>
    public async Task AwaitJobLive(string workflow, string batchId, Action<object> takeOutput, CancellationToken interrupt)
    {
        JObject workflowJson = Utilities.ParseToJson(workflow);
        int expectedNodes = workflowJson.Count;
        string id = Guid.NewGuid().ToString();
        ClientWebSocket socket = await ConnectWebsocket($"ws?clientId={id}");
        int nodesDone = 0;
        float curPercent;
        void yieldProgressUpdate()
        {
            takeOutput(new JObject()
            {
                ["batch_index"] = batchId,
                ["overall_percent"] = nodesDone / (float)expectedNodes,
                ["current_percent"] = curPercent
            });
        }
        try
        {
            workflow = $"{{\"prompt\": {workflow}, \"client_id\": \"{id}\"}}";
            Logs.Verbose($"Will use workflow: {workflow}");
            JObject promptResult = await PostJSONString("prompt", workflow, interrupt);
            Logs.Verbose($"ComfyUI prompt said: {promptResult}");
            if (promptResult.ContainsKey("error"))
            {
                Logs.Debug($"Error came from prompt: {workflow}");
                throw new InvalidDataException($"ComfyUI errored: {promptResult}");
            }
            string promptId = $"{promptResult["prompt_id"]}";
            long start = Environment.TickCount64;
            bool hasInterrupted = false;
            while (true)
            {
                byte[] output = await socket.ReceiveData(32 * 1024 * 1024, Program.GlobalProgramCancel);
                if (output is not null)
                {
                    if (Encoding.ASCII.GetString(output, 0, 8) == "{\"type\":")
                    {
                        JObject json = Utilities.ParseToJson(Encoding.UTF8.GetString(output));
                        Logs.Verbose($"ComfyUI Websocket said: {json.ToString(Formatting.None)}");
                        switch ($"{json["type"]}")
                        {
                            case "executing":
                                if ($"{json["data"]["node"]}" == "") // Not true null for some reason, so, ... this.
                                {
                                    goto endloop;
                                }
                                goto case "execution_cached";
                            case "execution_cached":
                                nodesDone++;
                                curPercent = 0;
                                yieldProgressUpdate();
                                break;
                            case "progress":
                                curPercent = json["data"].Value<float>("value") / json["data"].Value<float>("max");
                                yieldProgressUpdate();
                                break;
                            case "executed":
                                nodesDone = expectedNodes;
                                curPercent = 0;
                                yieldProgressUpdate();
                                break;
                            case "exection_start": // queuing
                            case "status": // queuing
                                break;
                            default:
                                Logs.Verbose($"Ignore type {json["type"]}");
                                break;
                        }
                    }
                    else
                    {
                        //Logs.Verbose($"ComfyUI Websocket sent raw data: {Encoding.ASCII.GetString(output, 0, Math.Min(output.Length, 32))}...");
                        long format = BitConverter.ToInt64(output, 0);
                        string formatLabel = format switch { 1 => "jpeg", 2 => "png", _ => "jpeg" };
                        takeOutput(new JObject()
                        {
                            ["batch_index"] = batchId,
                            ["preview"] = $"data:image/{formatLabel};base64," + Convert.ToBase64String(output, 8, output.Length - 8)
                        });
                    }
                }
                if (socket.CloseStatus.HasValue)
                {
                    return;
                }
                if (interrupt.IsCancellationRequested && !hasInterrupted)
                {
                    hasInterrupted = true;
                    Logs.Debug("ComfyUI Interrupt requested");
                    await HttpClient.PostAsync($"{Address}/interrupt", new StringContent(""), interrupt);
                }
            }
            endloop:
            JObject historyOut = await SendGet<JObject>($"history/{promptId}");
            if (!historyOut.Properties().IsEmpty())
            {
                foreach (Image image in await GetAllImagesForHistory(historyOut[promptId], interrupt))
                {
                    takeOutput(image);
                }
            }
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", interrupt);
        }
        finally
        {
            socket.Dispose();
        }
    }

    private async Task<Image[]> GetAllImagesForHistory(JToken output, CancellationToken interrupt)
    {
        Logs.Verbose($"ComfyUI history said: {output}");
        List<Image> outputs = new();
        foreach (JToken outData in output["outputs"].Values())
        {
            foreach (JToken outImage in outData["images"])
            {
                string fname = outImage["filename"].ToString();
                byte[] image = await(await HttpClient.GetAsync($"{Address}/view?filename={HttpUtility.UrlEncode(fname)}", interrupt)).Content.ReadAsByteArrayAsync(interrupt);
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

    public async Task<Image[]> AwaitJob(string workflow, CancellationToken interrupt)
    {
        workflow = $"{{\"prompt\": {workflow}}}";
        Logs.Verbose($"Will use workflow: {workflow}");
        JObject result = await PostJSONString("prompt", workflow, interrupt);
        Logs.Verbose($"ComfyUI prompt said: {result}");
        if (result.ContainsKey("error"))
        {
            Logs.Debug($"Error came from prompt: {workflow}");
            throw new InvalidDataException($"ComfyUI errored: {result}");
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
        return await GetAllImagesForHistory(output[promptId], interrupt);
    }

    public volatile int ImageIDDedup = 0;

    public override async Task<Image[]> Generate(T2IParamInput user_input)
    {
        List<Image> images = new();
        await GenerateLive(user_input, "0", output =>
        {
            if (output is Image img)
            {
                images.Add(img);
            }
        });
        return images.ToArray();
    }

    public static string CreateWorkflow(T2IParamInput user_input, Func<string, string> initImageFixer)
    {
        string workflow = null;
        if (user_input.TryGet(ComfyUIBackendExtension.CustomWorkflowParam, out string customWorkflowName))
        {
            string path = Utilities.FilePathForbidden.TrimToNonMatches(customWorkflowName).Replace(".", "");
            path = $"{ComfyUIBackendExtension.Folder}/CustomWorkflows/{path}.json";
            if (!File.Exists(path))
            {
                throw new InvalidDataException("Unrecognized ComfyUI Custom Workflow name.");
            }
            workflow = Encoding.UTF8.GetString(File.ReadAllBytes(path)).ParseToJson()["prompt"].ToString();
        }
        else if (user_input.TryGetRaw(ComfyUIBackendExtension.FakeRawInputType, out object workflowRaw))
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
        if (workflow is not null)
        {
            Logs.Verbose($"Will fill workflow {workflow}");
            workflow = StringConversionHelper.QuickSimpleTagFiller(initImageFixer(workflow), "${", "}", (tag) => {
                string fixedTag = Utilities.UnescapeJsonString(tag);
                string tagName = fixedTag.BeforeAndAfter(':', out string defVal);
                string tagBasic = tagName.BeforeAndAfter('+', out string tagExtra);
                string fillDynamic()
                {
                    T2IParamType type = T2IParamTypes.GetType(tagBasic, user_input) ?? throw new InvalidDataException($"Unknown param type request '{tagBasic}'");
                    if (!user_input.TryGetRaw(type, out object val) || val is null)
                    {
                        val = defVal;
                    }
                    if (type.Type == T2IParamDataType.INTEGER && type.NumberView == NumberViewType.SEED && long.Parse(val.ToString()) == -1)
                    {
                        return $"{Random.Shared.Next()}";
                    }
                    return val.ToString();
                }
                long fixSeed(long input)
                {
                    return input == -1 ? Random.Shared.Next() : input;
                }
                string filled = tagBasic switch
                {
                    "prompt" => user_input.Get(T2IParamTypes.Prompt),
                    "negative_prompt" => user_input.Get(T2IParamTypes.NegativePrompt),
                    "seed" => $"{fixSeed(user_input.Get(T2IParamTypes.Seed)) + (int.TryParse(tagExtra, out int add) ? add : 0)}",
                    "steps" => $"{user_input.Get(T2IParamTypes.Steps)}",
                    "width" => $"{user_input.Get(T2IParamTypes.Width)}",
                    "height" => $"{user_input.GetImageHeight()}",
                    "cfg_scale" => $"{user_input.Get(T2IParamTypes.CFGScale)}",
                    "subseed" => $"{user_input.Get(T2IParamTypes.VariationSeed)}",
                    "subseed_strength" => user_input.GetString(T2IParamTypes.VariationSeedStrength),
                    "init_image" => user_input.Get(T2IParamTypes.InitImage)?.AsBase64,
                    "init_image_strength" => user_input.GetString(T2IParamTypes.InitImageCreativity),
                    "comfy_sampler" or "comfyui_sampler" => user_input.GetString(ComfyUIBackendExtension.SamplerParam) ?? (string.IsNullOrWhiteSpace(defVal) ? "euler" : defVal),
                    "comfy_scheduler" or "comfyui_scheduler" => user_input.GetString(ComfyUIBackendExtension.SchedulerParam) ?? (string.IsNullOrWhiteSpace(defVal) ? "normal" : defVal),
                    "model" => user_input.Get(T2IParamTypes.Model).ToString(),
                    "prefix" => $"StableSwarmUI_{Random.Shared.Next():X4}_",
                    _ => fillDynamic()
                };
                filled ??= defVal;
                return Utilities.EscapeJsonString(filled);
            });
        }
        else
        {
            workflow = new WorkflowGenerator() { UserInput = user_input }.Generate().ToString();
            workflow = initImageFixer(workflow);
        }
        return workflow;
    }

    public override async Task GenerateLive(T2IParamInput user_input, string batchId, Action<object> takeOutput)
    {
        List<Action> completeSteps = new();
        string initImageFixer(string workflow) // TODO: This is a hack.
        {
            void TryApply(string key, Image img, bool resize)
            {
                string replaceMe = "${" + key + "}";
                if (workflow.Contains(key))
                {
                    int id = Interlocked.Increment(ref ImageIDDedup);
                    string fname = $"init_image_sui_backend_{BackendData.ID}_{id}.png";
                    Image fixedImage = resize ? img.Resize(user_input.Get(T2IParamTypes.Width), user_input.GetImageHeight()) : img;
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
                    workflow = workflow.Replace(replaceMe, fname);
                }
            }
            foreach ((string key, object val) in user_input.ValuesInput)
            {
                bool resize = !T2IParamTypes.TryGetType(key, out T2IParamType type, user_input) || type.ImageShouldResize;
                if (val is Image img)
                {
                    TryApply(key, img, resize);
                }
                else if (val is List<Image> imgs)
                {
                    for (int i = 0; i < imgs.Count; i++)
                    {
                        TryApply(key + "." + i, imgs[i], resize);
                    }
                }
            }
            return workflow;
        }
        string workflow = CreateWorkflow(user_input, initImageFixer);
        try
        {
            await AwaitJobLive(workflow, batchId, takeOutput, user_input.InterruptToken);
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
        await AwaitJob(workflow, Program.GlobalProgramCancel);
        CurrentModelName = model.Name;
        return true;
    }

    public override IEnumerable<string> SupportedFeatures => ComfyUIBackendExtension.FeaturesSupported;
}
