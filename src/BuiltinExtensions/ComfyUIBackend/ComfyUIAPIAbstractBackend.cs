
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

namespace StableSwarmUI.Builtin_ComfyUIBackend;

public abstract class ComfyUIAPIAbstractBackend : AbstractT2IBackend
{
    public abstract string Address { get; }

    /// <summary>Internal HTTP handler.</summary>
    public static HttpClient HttpClient = NetworkBackendUtils.MakeHttpClient();

    public JObject RawObjectInfo;

    public string ModelFolderFormat = null;

    public record class ReusableSocket(string ID, ClientWebSocket Socket);

    public ConcurrentQueue<ReusableSocket> ReusableSockets = new();

    public string WSID;

    public async Task LoadValueSet(double maxMinutes = 1)
    {
        Logs.Verbose($"Comfy backend {BackendData.ID} loading value set...");
        JObject result = await SendGet<JObject>("object_info", Utilities.TimedCancel(TimeSpan.FromMinutes(maxMinutes)));
        if (result.TryGetValue("error", out JToken errorToken))
        {
            Logs.Verbose($"Comfy backend {BackendData.ID} failed to load value set: {errorToken}");
            throw new Exception($"Remote error: {errorToken}");
        }
        Logs.Verbose($"Comfy backend {BackendData.ID} loaded value set, parsing...");
        RawObjectInfo = result;
        ConcurrentDictionary<string, List<string>> newModels = [];
        string firstBackSlash = null;
        void trackModels(string subtype, string node, string param)
        {
            if (RawObjectInfo.TryGetValue(node, out JToken loaderNode))
            {
                string[] modelList = loaderNode["input"]["required"][param][0].Select(t => (string)t).ToArray();
                firstBackSlash ??= modelList.FirstOrDefault(m => m.Contains('\\'));
                if (newModels.TryGetValue(subtype, out List<string> existingList))
                {
                    modelList = [.. modelList.Concat(existingList)];
                }
                newModels[subtype] = modelList.Select(m => m.Replace('\\', '/')).ToList();
            }
        }
        trackModels("Stable-Diffusion", "CheckpointLoaderSimple", "ckpt_name");
        trackModels("Stable-Diffusion", "TensorRTLoader", "unet_name");
        trackModels("LoRA", "LoraLoader", "lora_name");
        trackModels("VAE", "VAELoader", "vae_name");
        trackModels("ControlNet", "ControlNetLoader", "control_net_name");
        trackModels("ClipVision", "CLIPVisionLoader", "clip_name");
        trackModels("Embedding", "SwarmEmbedLoaderListProvider", "embed_name");
        Models = newModels;
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
        try
        {
            ComfyUIBackendExtension.AssignValuesFromRaw(RawObjectInfo);
        }
        catch (Exception ex)
        {
            Logs.Error($"Comfy backend {BackendData.ID} failed to load raw node backend info: {ex}");
        }
        Logs.Verbose($"Comfy backend {BackendData.ID} loaded value set and parsed.");
    }

    public abstract bool CanIdle { get; }

    public abstract int OverQueue { get; }

    public NetworkBackendUtils.IdleMonitor Idler = new();

    public async Task InitInternal(bool ignoreWebError)
    {
        MaxUsages = 1 + OverQueue;
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
        catch (Exception e)
        {
            if (!ignoreWebError)
            {
                throw;
            }
            Logs.Verbose($"Comfy backend {BackendData.ID} failed to load value set, but ignoring error: {e.GetType().Name}: {e.Message}");
        }
        Idler.Stop();
        if (CanIdle)
        {
            Idler.Backend = this;
            Idler.ValidateCall = () => SendGet<JObject>("object_info", Utilities.TimedCancel(TimeSpan.FromMinutes(1))).Wait();
            Idler.Start();
        }
    }

    /// <inheritdoc/>
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
    /// <param name="user_input">Original user input data.</param>
    /// <param name="interrupt">Interrupt token to use.</param>
    public async Task AwaitJobLive(string workflow, string batchId, Action<object> takeOutput, T2IParamInput user_input, CancellationToken interrupt)
    {
        if (interrupt.IsCancellationRequested)
        {
            return;
        }
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
                Logs.Debug($"Error came from prompt: {JObject.Parse(workflow).ToDenseDebugString(noSpacing: true)}");
                throw new InvalidDataException($"ComfyUI errored: {promptResult}");
            }
            string promptId = $"{promptResult["prompt_id"]}";
            long firstStep = 0;
            bool hasInterrupted = false;
            bool isReceivingOutputs = false;
            bool isExpectingVideo = false;
            string currentNode = "";
            bool isMe = false;
            while (true)
            {
                if (interrupt.IsCancellationRequested && !hasInterrupted)
                {
                    hasInterrupted = true;
                    Logs.Debug("ComfyUI Interrupt requested");
                    await HttpClient.PostAsync($"{Address}/interrupt", new StringContent(""), Program.GlobalProgramCancel);
                }
                byte[] output = await socket.ReceiveData(100 * 1024 * 1024, Program.GlobalProgramCancel);
                if (output is not null)
                {
                    if (Encoding.ASCII.GetString(output, 0, 8) == "{\"type\":")
                    {
                        JObject json = Utilities.ParseToJson(Encoding.UTF8.GetString(output));
                        if (Logs.MinimumLevel <= Logs.LogLevel.Verbose)
                        {
                            Logs.Verbose($"ComfyUI Websocket {batchId} said (isMe={isMe}): {json.ToString(Formatting.None)}");
                        }
                        string type = $"{json["type"]}";
                        if (!isMe)
                        {
                            if (type == "execution_start")
                            {
                                if ($"{json["data"]["prompt_id"]}" == promptId)
                                {
                                    isMe = true;
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }
                        switch (type)
                        {
                            case "executing":
                                string nodeId = $"{json["data"]["node"]}";
                                if (nodeId == "") // Not true null for some reason, so, ... this.
                                {
                                    goto endloop;
                                }
                                currentNode = nodeId;
                                goto case "execution_cached";
                            case "execution_cached":
                                nodesDone++;
                                curPercent = 0;
                                hasInterrupted = false;
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
                            case "execution_start":
                                if (firstStep == 0)
                                {
                                    firstStep = Environment.TickCount64;
                                }
                                break;
                            case "status": // queuing
                                break;
                            default:
                                Logs.Verbose($"Ignore type {json["type"]}");
                                break;
                        }
                    }
                    else
                    {
                        (string formatLabel, int index, int eventId) = ComfyRawWebsocketOutputToFormatLabel(output);
                        Logs.Verbose($"ComfyUI Websocket sent: {output.Length} bytes of image data as event {eventId} in format {formatLabel} to index {index}");
                        if (isReceivingOutputs)
                        {
                            Image.ImageType type = ComfyFormatLabelToImageType(formatLabel);
                            if (isExpectingVideo && type == Image.ImageType.IMAGE)
                            {
                                type = Image.ImageType.VIDEO;
                            }
                            bool isReal = true;
                            if (currentNode is not null && int.TryParse(currentNode, out int nodeIdNum) && ((nodeIdNum < 100 && nodeIdNum != 9) || nodeIdNum >= 50000))
                            {
                                // Reserved nodes that aren't the final output are intermediate outputs, or nodes in the 50,000+ range.
                                isReal = false;
                            }
                            if (Program.ServerSettings.AddDebugData)
                            {
                                user_input.ExtraMeta["debug_backend"] = new JObject()
                                {
                                    ["backend_type"] = BackendData.BackType.Name,
                                    ["backend_id"] = BackendData.ID,
                                    ["debug_internal_prompt"] = user_input.Get(T2IParamTypes.Prompt),
                                    ["backend_usages"] = BackendData.Usages,
                                    ["comfy_output_node"] = currentNode,
                                    ["comfy_is_real"] = isReal,
                                    ["comfy_img_type"] = $"{type}",
                                    ["comfy_event_id"] = eventId,
                                    ["comfy_index"] = index
                                };
                            }
                            takeOutput(new T2IEngine.ImageOutput() { Img = new Image(output[8..], type, formatLabel), IsReal = isReal, GenTimeMS = firstStep == 0 ? -1 : (Environment.TickCount64 - firstStep) });
                        }
                        else
                        {
                            string dataType = formatLabel switch
                            {
                                "jpg" => "image/jpeg",
                                "png" => "image/png",
                                "webp" => "image/webp",
                                "gif" => "image/gif",
                                "mp4" => "video/mp4",
                                "webm" => "video/webm",
                                _ => "image/jpeg"
                            };
                            takeOutput(new JObject()
                            {
                                ["batch_index"] = index == 0 || !int.TryParse(batchId, out int batchInt) ? batchId : batchInt + index,
                                ["preview"] = $"data:{dataType};base64," + Convert.ToBase64String(output, 8, output.Length - 8),
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
            }
            endloop:
            JObject historyOut = await SendGet<JObject>($"history/{promptId}");
            if (!historyOut.Properties().IsEmpty())
            {
                foreach (Image image in await GetAllImagesForHistory(historyOut[promptId], interrupt))
                {
                    if (Program.ServerSettings.AddDebugData)
                    {
                        user_input.ExtraMeta["debug_backend"] = new JObject()
                        {
                            ["backend_type"] = BackendData.BackType.Name,
                            ["backend_id"] = BackendData.ID,
                            ["debug_internal_prompt"] = user_input.Get(T2IParamTypes.Prompt),
                            ["backend_usages"] = BackendData.Usages,
                            ["comfy_output_history_prompt_id"] = promptId
                        };
                    }
                    takeOutput(new T2IEngine.ImageOutput() { Img = image, IsReal = true, GenTimeMS = firstStep == 0 ? -1 : (Environment.TickCount64 - firstStep) });
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

    public static (string, int, int) ComfyRawWebsocketOutputToFormatLabel(byte[] output)
    {
        int eventId = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(output, 0));
        int format = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(output, 4));
        int index = 0;
        if (format > 2)
        {
            index = (format >> 4) & 0xffff;
            format &= 7;
        }
        string formatLabel = format switch { 1 => "jpg", 2 => "png", 3 => "webp", 4 => "gif", 5 => "mp4", 6 => "webm", 7 => "mov", _ => "jpg" };
        return (formatLabel, index, eventId);
    }

    public static Image.ImageType ComfyFormatLabelToImageType(string formatLabel) => formatLabel switch
    {
        "jpg" => Image.ImageType.IMAGE,
        "png" => Image.ImageType.IMAGE,
        "webp" => Image.ImageType.IMAGE,
        "gif" => Image.ImageType.ANIMATION,
        "mp4" => Image.ImageType.VIDEO,
        "webm" => Image.ImageType.VIDEO,
        "mov" => Image.ImageType.VIDEO,
        _ => Image.ImageType.IMAGE
    };

    private async Task<Image[]> GetAllImagesForHistory(JToken output, CancellationToken interrupt)
    {
        if (Logs.MinimumLevel <= Logs.LogLevel.Verbose)
        {
            Logs.Verbose($"ComfyUI history said: {output.ToDenseDebugString()}");
        }
        if ((output as JObject).TryGetValue("status", out JToken status) && (status as JObject).TryGetValue("messages", out JToken messages))
        {
            foreach (JToken msg in messages)
            {
                if (msg[0].ToString() == "execution_error" && (msg[1] as JObject).TryGetValue("exception_message", out JToken actualMessage))
                {
                    throw new InvalidOperationException($"ComfyUI execution error: {actualMessage}");
                }
            }
        }
        List<Image> outputs = [];
        List<string> outputFailures = [];
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
                else if (ext == "mp4" || ext == "mov" || ext == "webm" || format.StartsWith("video/"))
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
        return [.. outputs];
    }

    /// <inheritdoc/>
    public override async Task<Image[]> Generate(T2IParamInput user_input)
    {
        List<Image> images = [];
        await GenerateLive(user_input, "0", output =>
        {
            if (output is Image img)
            {
                images.Add(img);
            }
        });
        return [.. images];
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
                JObject flowObj = ComfyUIWebAPI.ReadCustomWorkflow(customWorkflowName);
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
        }
        workflow = workflow?.Replace("\"%%_COMFYFIXME_${", "${").Replace("}_ENDFIXME_%%\"", "}");
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
                string getLoras()
                {
                    string[] loraNames = [.. Program.T2IModelSets["LoRA"].ListModelNamesFor(user_input.SourceSession)];
                    string[] matches = [.. user_input.Get(T2IParamTypes.Loras).Select(lora => T2IParamTypes.GetBestModelInList(lora, loraNames))];
                    if (matches.Any(m => string.IsNullOrWhiteSpace(m)))
                    {
                        throw new InvalidDataException("One or more LoRA models not found.");
                    }
                    return matches.JoinString(",");
                }
                string filled = tagBasic switch
                {
                    "stability_api_key" => user_input.SourceSession.User.GetGenericData("stability_api", "key") ?? throw new InvalidDataException("Stability API key not set - please go to the User tab to set it."),
                    "prompt" => user_input.Get(T2IParamTypes.Prompt),
                    "negative_prompt" => user_input.Get(T2IParamTypes.NegativePrompt),
                    "seed" => $"{fixSeed(user_input.Get(T2IParamTypes.Seed)) + (int.TryParse(tagExtra, out int add) ? add : 0)}",
                    "steps" => $"{user_input.Get(T2IParamTypes.Steps)}",
                    "width" => $"{user_input.Get(T2IParamTypes.Width)}",
                    "height" => $"{user_input.GetImageHeight()}",
                    "cfg_scale" => $"{user_input.Get(T2IParamTypes.CFGScale)}",
                    "subseed" => $"{user_input.Get(T2IParamTypes.VariationSeed)}",
                    "subseed_strength" => user_input.GetString(T2IParamTypes.VariationSeedStrength),
                    "init_image" => user_input.Get(T2IParamTypes.InitImage, null)?.AsBase64,
                    "init_image_strength" => user_input.GetString(T2IParamTypes.InitImageCreativity),
                    "comfy_sampler" or "comfyui_sampler" or "sampler" => user_input.GetString(ComfyUIBackendExtension.SamplerParam) ?? (string.IsNullOrWhiteSpace(defVal) ? "euler" : defVal),
                    "comfy_scheduler" or "comfyui_scheduler" or "scheduler" => user_input.GetString(ComfyUIBackendExtension.SchedulerParam) ?? (string.IsNullOrWhiteSpace(defVal) ? "normal" : defVal),
                    "model" => user_input.Get(T2IParamTypes.Model).ToString(ModelFolderFormat),
                    "prefix" => $"StableSwarmUI_{Random.Shared.Next():X4}_",
                    "loras" => getLoras(),
                    _ => fillDynamic()
                };
                filled ??= defVal;
                if (Logs.MinimumLevel <= Logs.LogLevel.Verbose)
                {
                    Logs.Verbose($"Filled tag '{tag}' with '{filled}'");
                }
                return Utilities.EscapeJsonString(filled);
            }, false);
            Logs.Verbose("Workflow filled.");
        }
        else
        {
            workflow = new WorkflowGenerator() { UserInput = user_input, ModelFolderFormat = ModelFolderFormat, Features = features ?? [] }.Generate().ToString();
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

    /// <inheritdoc/>
    public override async Task GenerateLive(T2IParamInput user_input, string batchId, Action<object> takeOutput)
    {
        List<Action> completeSteps = [];
        string initImageFixer(string workflow) // This is a hack, backup for if Swarm nodes are missing
        {
            void TryApply(string key, Image img, bool resize)
            {
                Image fixedImage = resize ? img.Resize(user_input.Get(T2IParamTypes.Width), user_input.GetImageHeight()) : img;
                if (key.Contains("swarmloadimageb") || key.Contains("swarminputimage"))
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
                if (val is Image img && !type.ImageAlwaysB64)
                {
                    TryApply(key, img, resize);
                }
                else if (val is List<Image> imgs && !type.ImageAlwaysB64)
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
            await AwaitJobLive(workflow, batchId, takeOutput, user_input, user_input.InterruptToken);
        }
        catch (Exception ex)
        {
            Logs.Verbose($"Error: {ex}");
            Logs.Debug($"Failed to process comfy workflow: {JObject.Parse(workflow).ToDenseDebugString(noSpacing: true)} for inputs {user_input}");
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

    public Task<JType> SendGet<JType>(string url) where JType : class
    {
        return SendGet<JType>(url, Program.GlobalProgramCancel);
    }

    public async Task<JType> SendGet<JType>(string url, CancellationToken token) where JType : class
    {
        return await NetworkBackendUtils.Parse<JType>(await HttpClient.GetAsync($"{Address}/{url}", token));
    }

    public async Task<JType> SendPost<JType>(string url, JObject payload) where JType : class
    {
        return await NetworkBackendUtils.Parse<JType>(await HttpClient.PostAsync($"{Address}/{url}", Utilities.JSONContent(payload)));
    }

    /// <inheritdoc/>
    public override async Task<bool> LoadModel(T2IModel model)
    {
        T2IParamInput input = new(null);
        input.Set(T2IParamTypes.Model, model);
        input.Set(T2IParamTypes.Steps, 0);
        input.Set(T2IParamTypes.Width, 256);
        input.Set(T2IParamTypes.Height, 256);
        input.Set(T2IParamTypes.Prompt, "(load the model please)");
        input.Set(T2IParamTypes.NegativePrompt, "");
        input.Set(T2IParamTypes.Images, 1);
        input.Set(T2IParamTypes.CFGScale, 7);
        input.Set(T2IParamTypes.Seed, 1);
        WorkflowGenerator wg = new() { UserInput = input, ModelFolderFormat = ModelFolderFormat, Features = [.. SupportedFeatures] };
        JObject workflow = wg.Generate();
        await AwaitJobLive(workflow.ToString(), "0", _ => { }, new(null), Program.GlobalProgramCancel);
        CurrentModelName = model.Name;
        return true;
    }

    /// <inheritdoc/>
    public override async Task<bool> FreeMemory(bool systemRam)
    {
        await SendPost<string>("free", new JObject() { ["unload_models"] = true, ["free_memory"] = systemRam });
        return true;
    }

    /// <inheritdoc/>
    public override IEnumerable<string> SupportedFeatures => ComfyUIBackendExtension.FeaturesSupported.Append(ModelFolderFormat == "\\" ? "folderbackslash" : "folderslash");
}
