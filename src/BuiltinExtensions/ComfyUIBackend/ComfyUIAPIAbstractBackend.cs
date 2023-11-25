
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
using System.Buffers.Binary;
using System.Net.Sockets;

namespace StableSwarmUI.Builtin_ComfyUIBackend;

public abstract class ComfyUIAPIAbstractBackend : AbstractT2IBackend
{
    public abstract string Address { get; }

    /// <summary>Internal HTTP handler.</summary>
    public HttpClient HttpClient = NetworkBackendUtils.MakeHttpClient();

    public JObject RawObjectInfo;

    public string ModelFolderFormat = null;

    public record class ReusableSocket(string ID, ClientWebSocket Socket);

    public ConcurrentQueue<ReusableSocket> ReusableSockets = new();

    public string WSID;

    public async Task LoadValueSet()
    {
        JObject result = await SendGet<JObject>("object_info");
        if (result.TryGetValue("error", out JToken errorToken))
        {
            throw new Exception($"Remote error: {errorToken}");
        }
        RawObjectInfo = result;
        Models ??= new();
        string firstBackSlash = null;
        void trackModels(string subtype, string node, string param)
        {
            if (RawObjectInfo.TryGetValue(node, out JToken loaderNode))
            {
                string[] modelList = loaderNode["input"]["required"][param][0].Select(t => (string)t).ToArray();
                firstBackSlash ??= modelList.FirstOrDefault(m => m.Contains('\\'));
                Models[subtype] = modelList.Select(m => m.Replace('\\', '/')).ToList();
            }
        }
        trackModels("Stable-Diffusion", "CheckpointLoaderSimple", "ckpt_name");
        trackModels("LoRA", "LoraLoader", "lora_name");
        trackModels("VAE", "VAELoader", "vae_name");
        trackModels("ControlNet", "ControlNetLoader", "control_net_name");
        trackModels("ClipVision", "CLIPVisionLoader", "clip_name");
        trackModels("Embedding", "SwarmEmbedLoaderListProvider", "embed_name");
        if (firstBackSlash is not null)
        {
            ModelFolderFormat = "\\";
            Logs.Debug($"Comfy backend {BackendData.ID} using model folder format: backslash \\ due to model {firstBackSlash}");
        }
        else
        {
            ModelFolderFormat = "/";
            Logs.Debug($"Comfy backend {BackendData.ID} using model folder format: forward slash / as no backslash was found");
        }
        ComfyUIBackendExtension.AssignValuesFromRaw(RawObjectInfo);
    }

    public abstract bool CanIdle { get; }

    public NetworkBackendUtils.IdleMonitor Idler = new();

    public async Task InitInternal(bool ignoreWebError)
    {
        MaxUsages = 2;
        if (string.IsNullOrWhiteSpace(Address))
        {
            Status = BackendStatus.DISABLED;
            return;
        }
        Status = BackendStatus.LOADING;
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
        Idler.Stop();
        if (CanIdle)
        {
            Idler.Backend = this;
            Idler.ValidateCall = () => SendGet<JObject>("object_info").Wait();
            Idler.Start();
        }
    }

    public override async Task Shutdown()
    {
        Logs.Info($"ComfyUI backend {BackendData.ID} shutting down...");
        while (ReusableSockets.TryDequeue(out ReusableSocket socket))
        {
            try
            {
                if (socket.Socket.State == WebSocketState.Open)
                {
                    await socket.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", Utilities.TimedCancel(TimeSpan.FromSeconds(5)));
                }
                socket.Socket.Dispose();
            }
            catch (Exception ex)
            {
                Logs.Verbose($"ComfyUI backend {BackendData.ID} failed to close websocket: {ex}");
            }
        }
        Idler.Stop();
        Status = BackendStatus.DISABLED;
    }

    public virtual void PostResultCallback(string filename)
    {
    }

    /// <summary>Runs a job with live feedback (progress updates, previews, etc.)</summary>
    /// <param name="workflow">The workflow JSON to use.</param>
    /// <param name="batchId">Local batch-ID for this generation.</param>
    /// <param name="takeOutput">Takes an output object: Image for final images, JObject for anything else.</param>
    /// <param name="interrupt">Interrupt token to use.</param>
    public async Task AwaitJobLive(string workflow, string batchId, Action<object> takeOutput, CancellationToken interrupt)
    {
        Logs.Verbose("Will await a job, do parse...");
        JObject workflowJson = Utilities.ParseToJson(workflow);
        Logs.Verbose("JSON parsed.");
        int expectedNodes = workflowJson.Count;
        string id = null;
        ClientWebSocket socket = null;
        try
        {
            while (ReusableSockets.TryDequeue(out ReusableSocket oldSocket))
            {
                if (oldSocket.Socket.State == WebSocketState.Open)
                {
                    Logs.Verbose("Reuse existing websocket");
                    id = oldSocket.ID;
                    socket = oldSocket.Socket;
                    break;
                }
                else
                {
                    oldSocket.Socket.Dispose();
                }
            }
            if (socket is null)
            {
                Logs.Verbose("Need to connect a websocket...");
                id = Guid.NewGuid().ToString();
                socket = await NetworkBackendUtils.ConnectWebsocket(Address, $"ws?clientId={id}");
                Logs.Verbose("Connected.");
            }
        }
        catch (Exception ex)
        {
            Logs.Verbose($"Websocket comfy connection failed: {ex}");
            if (CanIdle)
            {
                Status = BackendStatus.IDLE;
                throw new PleaseRedirectException();
            }
            throw;
        }
        int nodesDone = 0;
        float curPercent = 0;
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
            if (Logs.MinimumLevel <= Logs.LogLevel.Verbose)
            {
                Logs.Verbose($"Will use workflow: {JObject.Parse(workflow).ToDenseDebugString()}");
            }
            JObject promptResult = await HttpClient.PostJSONString($"{Address}/prompt", workflow, interrupt);
            if (Logs.MinimumLevel <= Logs.LogLevel.Verbose)
            {
                Logs.Verbose($"ComfyUI prompt said: {promptResult.ToDenseDebugString()}");
            }
            if (promptResult.ContainsKey("error"))
            {
                Logs.Debug($"Error came from prompt: {workflow}");
                throw new InvalidDataException($"ComfyUI errored: {promptResult}");
            }
            string promptId = $"{promptResult["prompt_id"]}";
            long start = Environment.TickCount64;
            bool hasInterrupted = false;
            bool isReceivingOutputs = false;
            bool isExpectingVideo = false;
            while (true)
            {
                byte[] output = await socket.ReceiveData(100 * 1024 * 1024, Program.GlobalProgramCancel);
                if (output is not null)
                {
                    if (Encoding.ASCII.GetString(output, 0, 8) == "{\"type\":")
                    {
                        JObject json = Utilities.ParseToJson(Encoding.UTF8.GetString(output));
                        if (Logs.MinimumLevel <= Logs.LogLevel.Verbose)
                        {
                            Logs.Verbose($"ComfyUI Websocket said: {json.ToString(Formatting.None)}");
                        }
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
                                int max = json["data"].Value<int>("max");
                                curPercent = json["data"].Value<float>("value") / max;
                                isReceivingOutputs = max == 12345 || max == 12346;
                                isExpectingVideo = max == 12346;
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
                        int eventId = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(output, 0));
                        int format = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(output, 4));
                        int index = 0;
                        if (format > 2)
                        {
                            index = (format >> 4) & 0xffff;
                            format &= 7;
                        }
                        string formatLabel = format switch { 1 => "jpeg", 2 => "png", 3 => "webp", _ => "jpeg" };
                        Logs.Verbose($"ComfyUI Websocket sent: {output.Length} bytes of image data as event {eventId} in format {format} ({formatLabel}) to index {index}");
                        if (isReceivingOutputs)
                        {
                            takeOutput(new Image(output[8..], isExpectingVideo ? Image.ImageType.VIDEO : Image.ImageType.IMAGE, formatLabel == "jpeg" ? "jpg" : formatLabel));
                            index++;
                        }
                        else
                        {
                            takeOutput(new JObject()
                            {
                                ["batch_index"] = index == 0 || !int.TryParse(batchId, out int batchInt) ? $"{index}{batchId}" : batchInt + index,
                                ["preview"] = $"data:image/{formatLabel};base64," + Convert.ToBase64String(output, 8, output.Length - 8),
                                ["overall_percent"] = nodesDone / (float)expectedNodes,
                                ["current_percent"] = curPercent
                            });
                        }
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
                    await HttpClient.PostAsync($"{Address}/interrupt", new StringContent(""), Program.GlobalProgramCancel);
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
        }
        catch (Exception)
        {
            if (CanIdle)
            {
                Status = BackendStatus.IDLE;
            }
            throw;
        }
        finally
        {
            ReusableSockets.Enqueue(new(id, socket));
        }
    }

    private async Task<Image[]> GetAllImagesForHistory(JToken output, CancellationToken interrupt)
    {
        if (Logs.MinimumLevel <= Logs.LogLevel.Verbose)
        {
            Logs.Verbose($"ComfyUI history said: {output.ToDenseDebugString()}");
        }
        List<Image> outputs = new();
        List<string> outputFailures = new();
        foreach (JToken outData in output["outputs"].Values())
        {
            if (outData is null)
            {
                Logs.Debug($"null output data from ComfyUI server: {output.ToDenseDebugString()}");
                outputFailures.Add($"Null output block (???)");
                continue;
            }
            async Task LoadImage(JObject outImage, Image.ImageType type)
            {
                string imType = "output";
                string fname = outImage["filename"].ToString();
                if ($"{outImage["type"]}" == "temp")
                {
                    imType = "temp";
                }
                string ext = fname.AfterLast('.');
                string format = (outImage.TryGetValue("format", out JToken formatTok) ? formatTok.ToString() : "") ?? "";
                if (ext == "gif")
                {
                    type = Image.ImageType.ANIMATION;
                }
                else if (ext == "mp4" || ext == "webm" || format.StartsWith("video/"))
                {
                    type = Image.ImageType.VIDEO;
                }
                byte[] image = await(await HttpClient.GetAsync($"{Address}/view?filename={HttpUtility.UrlEncode(fname)}&type={imType}", interrupt)).Content.ReadAsByteArrayAsync(interrupt);
                if (image == null || image.Length == 0)
                {
                    Logs.Error($"Invalid/null/empty image data from ComfyUI server for '{fname}', under {outData.ToDenseDebugString()}");
                    return;
                }
                outputs.Add(new Image(image, type, ext));
                PostResultCallback(fname);
            }
            if (outData["images"] is not null)
            {
                foreach (JToken outImage in outData["images"])
                {
                    await LoadImage(outImage as JObject, Image.ImageType.IMAGE);
                }
            }
            else if (outData["gifs"] is not null)
            {
                foreach (JToken outGif in outData["gifs"])
                {
                    await LoadImage(outGif as JObject, Image.ImageType.ANIMATION);
                }
            }
            else
            {
                Logs.Debug($"invalid/empty output data from ComfyUI server: {outData.ToDenseDebugString()}");
                outputFailures.Add($"Invalid/empty output block");
            }
        }
        if (output.IsEmpty())
        {
            if (outputFailures.Any())
            {
                Logs.Warning($"Comfy backend gave no valid output, but did give unrecognized outputs (enable Debug logs for more details): {outputFailures.JoinString(", ")}");
            }
            else
            {
                Logs.Warning($"Comfy backend gave no valid output");
            }
        }
        return outputs.ToArray();
    }

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

    public static string CreateWorkflow(T2IParamInput user_input, Func<string, string> initImageFixer, string ModelFolderFormat = null, HashSet<string> features = null)
    {
        string workflow = null;
        // note: gently break any standard embed with a space, *require* swarm format embeds, as comfy's raw syntax has unwanted behaviors
        user_input.ProcessPromptEmbeds(x => $"embedding:{x.Replace("/", ModelFolderFormat)}", p => p.Replace("embedding:", "embedding :", StringComparison.OrdinalIgnoreCase));
        if (user_input.TryGet(ComfyUIBackendExtension.CustomWorkflowParam, out string customWorkflowName))
        {
            if (customWorkflowName.StartsWith("PARSED%"))
            {
                workflow = customWorkflowName["PARSED%".Length..].After("%");
            }
            else
            {
                JObject flowObj = ComfyUIBackendExtension.ReadCustomWorkflow(customWorkflowName);
                if (flowObj.ContainsKey("error"))
                {
                    throw new InvalidDataException("Unrecognized ComfyUI Custom Workflow name.");
                }
                workflow = flowObj["prompt"].ToString();
            }
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
        if (workflow is not null && !user_input.Get(T2IParamTypes.ControlNetPreviewOnly))
        {
            Logs.Verbose("Will fill a workflow...");
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
                    if (type.Type == T2IParamDataType.INTEGER && type.ViewType == ParamViewType.SEED && long.Parse(val.ToString()) == -1)
                    {
                        return $"{Random.Shared.Next()}";
                    }
                    if (val is T2IModel model)
                    {
                        return model.ToString(ModelFolderFormat);
                    }
                    else if (val is Image image)
                    {
                        return image.AsBase64;
                    }
                    else if (val is List<string> list)
                    {
                        return list.JoinString(",");
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
                    "comfy_sampler" or "comfyui_sampler" or "sampler" => user_input.GetString(ComfyUIBackendExtension.SamplerParam) ?? (string.IsNullOrWhiteSpace(defVal) ? "euler" : defVal),
                    "comfy_scheduler" or "comfyui_scheduler" or "scheduler" => user_input.GetString(ComfyUIBackendExtension.SchedulerParam) ?? (string.IsNullOrWhiteSpace(defVal) ? "normal" : defVal),
                    "model" => user_input.Get(T2IParamTypes.Model).ToString(ModelFolderFormat),
                    "prefix" => $"StableSwarmUI_{Random.Shared.Next():X4}_",
                    _ => fillDynamic()
                };
                filled ??= defVal;
                return Utilities.EscapeJsonString(filled);
            }, false);
            Logs.Verbose("Workflow filled.");
        }
        else
        {
            workflow = new WorkflowGenerator() { UserInput = user_input, ModelFolderFormat = ModelFolderFormat, Features = features ?? new() }.Generate().ToString();
            workflow = initImageFixer(workflow);
        }
        return workflow;
    }

    public volatile int ImageIDDedup = 0;

    /// <summary>Returns true if the file will be deleted properly (and ID should not be reused as it may conflict), or false if it can't be deleted (and thus the ID should be reused to reduce amount of images getting stored).</summary>
    public virtual bool RemoveInputFile(string filename)
    {
        return false;
    }

    public override async Task GenerateLive(T2IParamInput user_input, string batchId, Action<object> takeOutput)
    {
        List<Action> completeSteps = new();
        string initImageFixer(string workflow) // This is a hack, backup for if Swarm nodes are missing
        {
            void TryApply(string key, Image img, bool resize)
            {
                Image fixedImage = resize ? img.Resize(user_input.Get(T2IParamTypes.Width), user_input.GetImageHeight()) : img;
                if (key.Contains("swarmloadimageb"))
                {
                    user_input.ValuesInput[key] = fixedImage;
                    return;
                }
                int index = workflow.IndexOf("${" + key);
                while (index != -1)
                {
                    char symbol = workflow[index + key.Length + 2];
                    if (symbol != '}' && symbol != ':')
                    {
                        index = workflow.IndexOf("${" + key, index + 1);
                        continue;
                    }
                    Logs.Debug($"Uploading image for '{key}' to Comfy server's file folder... are you missing the Swarm-Comfy nodes?");
                    int id = Interlocked.Increment(ref ImageIDDedup);
                    string fname = $"init_image_sui_backend_{BackendData.ID}_{id}.png";
                    MultipartFormDataContent content = new()
                    {
                        { new ByteArrayContent(fixedImage.ImageData), "image", fname },
                        { new StringContent("true"), "overwrite" }
                    };
                    HttpClient.PostAsync($"{Address}/upload/image", content).Wait();
                    completeSteps.Add(() =>
                    {
                        if (!RemoveInputFile(fname))
                        {
                            Interlocked.Decrement(ref ImageIDDedup);
                        }
                    });
                    workflow = workflow[0..index] + fname + workflow[(workflow.IndexOf('}', index) + 1)..];
                    index = workflow.IndexOf("${" + key);
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
        string workflow = CreateWorkflow(user_input, initImageFixer, ModelFolderFormat, SupportedFeatures.ToHashSet());
        try
        {
            await AwaitJobLive(workflow, batchId, takeOutput, user_input.InterruptToken);
        }
        catch (Exception ex)
        {
            Logs.Verbose($"Error: {ex}");
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
        string workflow = ComfyUIBackendExtension.Workflows["just_load_model"].Replace("${model:error_missing_model}", Utilities.EscapeJsonString(model.ToString(ModelFolderFormat)));
        await AwaitJobLive(workflow, "0", _ => { }, Program.GlobalProgramCancel);
        CurrentModelName = model.Name;
        return true;
    }

    public override IEnumerable<string> SupportedFeatures => ComfyUIBackendExtension.FeaturesSupported.Append(ModelFolderFormat == "\\" ? "folderbackslash" : "folderslash");
}
