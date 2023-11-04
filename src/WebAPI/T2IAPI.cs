using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.Data;
using System.IO;
using System.Net.WebSockets;
using System.Text.RegularExpressions;

namespace StableSwarmUI.WebAPI;

/// <summary>Text-to-Image API routes.</summary>
public static class T2IAPI
{
    public static void Register()
    {
        API.RegisterAPICall(GenerateText2Image);
        API.RegisterAPICall(GenerateText2ImageWS);
        API.RegisterAPICall(ListImages);
        API.RegisterAPICall(DeleteImage);
        API.RegisterAPICall(ListModels);
        API.RegisterAPICall(DescribeModel);
        API.RegisterAPICall(ListLoadedModels);
        API.RegisterAPICall(TriggerRefresh);
        API.RegisterAPICall(SelectModel);
        API.RegisterAPICall(SelectModelWS);
        API.RegisterAPICall(EditModelMetadata);
        API.RegisterAPICall(ListT2IParams);
    }

    /// <summary>API route to generate images with WebSocket updates.</summary>
    public static async Task<JObject> GenerateText2ImageWS(WebSocket socket, Session session, int images, JObject rawInput)
    {
        await API.RunWebsocketHandlerCallWS(GenT2I_Internal, session, (images, rawInput), socket);
        await socket.SendJson(BasicAPIFeatures.GetCurrentStatusRaw(session), API.WebsocketTimeout);
        return null;
    }

    /// <summary>API route to generate images directly as HTTP.</summary>
    public static async Task<JObject> GenerateText2Image(Session session, int images, JObject rawInput)
    {
        List<JObject> outputs = await API.RunWebsocketHandlerCallDirect(GenT2I_Internal, session, (images, rawInput));
        Dictionary<int, string> imageOutputs = new();
        int[] discards = null;
        foreach (JObject obj in outputs)
        {
            if (obj.ContainsKey("error"))
            {
                return obj;
            }
            if (obj.TryGetValue("image", out JToken image) && obj.TryGetValue("batch_index", out JToken index))
            {
                imageOutputs.Add((int)index, image.ToString());
            }
            if (obj.TryGetValue("discard_indices", out JToken discard))
            {
                discards = discard.Values<int>().ToArray();
            }
        }
        if (discards != null)
        {
            foreach (int x in discards)
            {
                imageOutputs.Remove(x);
            }
        }
        return new JObject() { ["images"] = new JArray(imageOutputs.Values.ToArray()) };
    }

    /// <summary>Helper util to take a user-supplied JSON object of parameter data and turn it into a valid T2I request object.</summary>
    public static T2IParamInput RequestToParams(Session session, JObject rawInput)
    {
        T2IParamInput user_input = new(session);
        foreach ((string key, JToken val) in rawInput)
        {
            if (T2IParamTypes.TryGetType(key, out _, user_input))
            {
                T2IParamTypes.ApplyParameter(key, val.ToString(), user_input);
            }
        }
        if (rawInput.TryGetValue("presets", out JToken presets))
        {
            foreach (JToken presetName in presets.Values())
            {
                T2IPreset presetObj = session.User.GetPreset(presetName.ToString());
                presetObj.ApplyTo(user_input);
            }
        }
        user_input.NormalizeSeeds();
        return user_input;
    }

    /// <summary>Internal route for generating images.</summary>
    public static async Task GenT2I_Internal(Session session, (int, JObject) input, Action<JObject> output, bool isWS)
    {
        (int images, JObject rawInput) = input;
        using Session.GenClaim claim = session.Claim(gens: images);
        void setError(string message)
        {
            Logs.Debug($"Refused to generate image for {session.User.UserID}: {message}");
            output(new JObject() { ["error"] = message });
            claim.LocalClaimInterrupt.Cancel();
        }
        T2IParamInput user_input;
        try
        {
            user_input = RequestToParams(session, rawInput);
        }
        catch (InvalidOperationException ex)
        {
            setError(ex.Message);
            return;
        }
        catch (InvalidDataException ex)
        {
            setError(ex.Message);
            return;
        }
        List<T2IEngine.ImageInBatch> imageSet = new();
        List<Task> tasks = new();
        void removeDoneTasks()
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].IsCompleted)
                {
                    if (tasks[i].IsFaulted)
                    {
                        Logs.Error($"Image generation failed: {tasks[i].Exception}");
                    }
                    tasks.RemoveAt(i--);
                }
            }
        }
        int max_degrees = session.User.Restrictions.CalcMaxT2ISimultaneous;
        List<int> discard = new();
        int numExtra = 0;
        int batchSizeExpected = user_input.Get(T2IParamTypes.BatchSize, 1);
        for (int i = 0; i < images && !claim.ShouldCancel; i++)
        {
            removeDoneTasks();
            while (tasks.Count > max_degrees)
            {
                await Task.WhenAny(tasks);
                removeDoneTasks();
            }
            if (claim.ShouldCancel)
            {
                break;
            }
            int imageIndex = i * batchSizeExpected;
            T2IParamInput thisParams = user_input.Clone();
            if (thisParams.TryGet(T2IParamTypes.VariationSeed, out long varSeed) && thisParams.Get(T2IParamTypes.VariationSeedStrength) > 0)
            {
                thisParams.Set(T2IParamTypes.VariationSeed, varSeed + imageIndex);
            }
            else
            {
                thisParams.Set(T2IParamTypes.Seed, thisParams.Get(T2IParamTypes.Seed) + imageIndex);
            }
            int numCalls = 0;
            tasks.Add(Task.Run(() => T2IEngine.CreateImageTask(thisParams, $"{imageIndex}", claim, output, setError, isWS, Program.ServerSettings.Backends.PerRequestTimeoutMinutes,
                (image, metadata) =>
                {
                    int actualIndex = imageIndex + numCalls;
                    numCalls++;
                    if (numCalls > batchSizeExpected)
                    {
                        actualIndex = images * batchSizeExpected + Interlocked.Increment(ref numExtra);
                    }
                    (string url, string filePath) = thisParams.Get(T2IParamTypes.DoNotSave, false) ? (session.GetImageB64(image), null) : session.SaveImage(image, actualIndex, thisParams, metadata);
                    if (url == "ERROR")
                    {
                        setError($"Server failed to save an image.");
                        return;
                    }
                    lock (imageSet)
                    {
                        imageSet.Add(new(image, () =>
                        {
                            if (filePath is not null && File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }
                            discard.Add(actualIndex);
                        }));
                    }
                    output(new JObject() { ["image"] = url, ["batch_index"] = $"{actualIndex}", ["metadata"] = string.IsNullOrWhiteSpace(metadata) ? null : metadata });
                })));
            Task.Delay(20).Wait(); // Tiny few-ms delay to encourage tasks retaining order.
        }
        while (tasks.Any())
        {
            await Task.WhenAny(tasks);
            removeDoneTasks();
        }
        T2IEngine.PostBatchEvent?.Invoke(new(user_input, imageSet.ToArray()));
        output(new JObject() { ["discard_indices"] = JToken.FromObject(discard) });
    }

    public static HashSet<string> ImageExtensions = new() { "png", "jpg", "html", "gif", "webm", "mp4" };

    // TODO: Configurable limit
    /// <summary>API route to get a list of available history images.</summary>
    private static JObject GetListAPIInternal(Session session, string path, string root, HashSet<string> extensions, Func<string, bool> isAllowed, Func<string, string, JObject> valToObj, int depth, int limit = 1000)
    {
        (path, string consoleError, string userError) = WebServer.CheckFilePath(root, path);
        if (consoleError is not null)
        {
            Logs.Error(consoleError);
            return new JObject() { ["error"] = userError };
        }
        try
        {
            if (!Directory.Exists(path))
            {
                return new JObject()
                {
                    ["folders"] = new JArray(),
                    ["files"] = new JArray()
                };
            }
            List<string> dirs = new();
            List<string> finalDirs = new();
            void addDirs(string dir, int subDepth)
            {
                if (dir != "")
                {
                    (subDepth == 0 ? finalDirs : dirs).Add(dir);
                }
                if (subDepth > 0)
                {
                    foreach (string subDir in Directory.EnumerateDirectories(path + "/" + dir).Select(Path.GetFileName).OrderDescending())
                    {
                        string subPath = dir == "" ? subDir : dir + "/" + subDir;
                        if (isAllowed(subPath))
                        {
                            addDirs(subPath, subDepth - 1);
                        }
                    }
                }
            }
            addDirs("", depth);
            List<JObject> files = new();
            foreach (string folder in dirs.Append(""))
            {
                string prefix = folder == "" ? "" : folder + "/";
                List<string> subFiles = Directory.EnumerateFiles($"{path}/{prefix}").Take(limit).ToList();
                files.AddRange(subFiles.Where(isAllowed).Where(f => extensions.Contains(f.AfterLast('.'))).Select(f => f.Replace('\\', '/')).Select(f => valToObj(f, prefix + f.AfterLast('/'))).ToList());
                limit -= subFiles.Count;
                if (limit <= 0)
                {
                    break;
                }
            }
            return new JObject()
            {
                ["folders"] = JToken.FromObject(dirs.Union(finalDirs).ToList()),
                ["files"] = JToken.FromObject(files)
            };
        }
        catch (Exception ex)
        {
            if (ex is FileNotFoundException || ex is DirectoryNotFoundException || ex is PathTooLongException)
            {
                return new JObject() { ["error"] = "404, path not found." };
            }
            else
            {
                Logs.Error($"Error reading file list: {ex}");
                return new JObject() { ["error"] = "Error reading file list." };
            }
        }
    }

    /// <summary>API route to delete an image.</summary>
    public static async Task<JObject> DeleteImage(Session session, string path)
    {
        string origPath = path;
        string root = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, session.User.OutputDirectory);
        (path, string consoleError, string userError) = WebServer.CheckFilePath(root, path);
        if (consoleError is not null)
        {
            Logs.Error(consoleError);
            return new JObject() { ["error"] = userError };
        }
        if (!File.Exists(path))
        {
            Logs.Warning($"User {session.User.UserID} tried to delete image path '{origPath}' which maps to '{path}', but cannot as the image does not exist.");
            return new JObject() { ["error"] = "That file does not exist, cannot delete." };
        }
        File.Delete(path);
        ImageMetadataTracker.RemoveMetadataFor(path);
        return new JObject() { ["success"] = true };
    }

    /// <summary>API route to get a list of available history images.</summary>
    public static async Task<JObject> ListImages(Session session, string path, int depth)
    {
        string root = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, session.User.OutputDirectory);
        return GetListAPIInternal(session, path, root, ImageExtensions, f => true, (file, name) => new JObject()
        {
            ["src"] = name,
            ["metadata"] = ImageMetadataTracker.GetMetadataFor(file)
        }, depth);
    }

    public static Dictionary<string, JObject> InternalExtraModels(string subtype)
    {
        SwarmSwarmBackend[] backends = Program.Backends.RunningBackendsOfType<SwarmSwarmBackend>().Where(b => b.RemoteModels is not null).ToArray();
        IEnumerable<Dictionary<string, JObject>> sets = backends.Select(b => b.RemoteModels.GetValueOrDefault(subtype)).Where(b => b is not null);
        if (sets.IsEmpty())
        {
            return new();
        }
        return sets.Aggregate((a, b) => a.Union(b).PairsToDictionary(false));
    }

    /// <summary>API route to describe a single model.</summary>
    public static async Task<JObject> DescribeModel(Session session, string modelName, string subtype = "Stable-Diffusion")
    {
        if (!Program.T2IModelSets.TryGetValue(subtype, out T2IModelHandler handler))
        {
            return new JObject() { ["error"] = "Invalid sub-type." };
        }
        modelName = modelName.Replace('\\', '/');
        while (modelName.Contains("//"))
        {
            modelName = modelName.Replace("//", "/");
        }
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (allowed is null || allowed.IsMatch(modelName))
        {
            if (handler.Models.TryGetValue(modelName, out T2IModel model))
            {
                return new JObject() { ["model"] = model.ToNetObject() };
            }
            else if (InternalExtraModels(subtype).TryGetValue(modelName, out JObject remoteModel))
            {
                return new JObject() { ["model"] = remoteModel };
            }
        }
        Logs.Debug($"Request for model {modelName} rejected as not found.");
        return new JObject() { ["error"] = "Model not found." };
    }

    /// <summary>API route to get a list of available models.</summary>
    public static async Task<JObject> ListModels(Session session, string path, int depth, string subtype = "Stable-Diffusion")
    {
        if (!Program.T2IModelSets.TryGetValue(subtype, out T2IModelHandler handler))
        {
            return new JObject() { ["error"] = "Invalid sub-type." };
        }
        depth = Math.Clamp(depth, 1, 10);
        path = path.Replace('\\', '/');
        if (path != "")
        {
            path += '/';
        }
        while (path.Contains("//"))
        {
            path = path.Replace("//", "/");
        }
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        HashSet<string> folders = new();
        List<JObject> files = new();
        HashSet<string> dedup = new();
        bool tryMatch(string name)
        {
            if (!name.StartsWith(path) || name.Length <= path.Length || (allowed is not null && !allowed.IsMatch(name)))
            {
                return false;
            }
            string part = name[path.Length..];
            int slashes = part.CountCharacter('/');
            if (slashes > 0)
            {
                string folderPart = part.BeforeLast('/');
                string[] subfolders = folderPart.Split('/');
                for (int i = 1; i <= depth && i <= subfolders.Length; i++)
                {
                    folders.Add(string.Join('/', subfolders[..i]));
                }
            }
            return slashes < depth && dedup.Add(name);
        }
        foreach (T2IModel possible in handler.Models.Values)
        {
            if (tryMatch(possible.Name))
            {
                files.Add(possible.ToNetObject());
            }
        }
        foreach ((string name, JObject possible) in InternalExtraModels(subtype))
        {
            if (tryMatch(name))
            {
                files.Add(possible);
            }
        }
        return new JObject()
        {
            ["folders"] = JToken.FromObject(folders.ToList()),
            ["files"] = JToken.FromObject(files)
        };
    }

    /// <summary>API route to get a list of currently loaded Stable-Diffusion models.</summary>
    public static async Task<JObject> ListLoadedModels(Session session)
    {
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        List<T2IModel> matches = Program.MainSDModels.Models.Values.Where(m => m.AnyBackendsHaveLoaded && (allowed is null || allowed.IsMatch(m.Name))).ToList();
        return new JObject()
        {
            ["models"] = JToken.FromObject(matches.Select(m => m.ToNetObject()).ToList())
        };
    }

    /// <summary>API route to trigger a reload of the model list.</summary>
    public static async Task<JObject> TriggerRefresh(Session session)
    {
        Logs.Verbose($"User {session.User.UserID} triggered a data refresh");
        Program.ModelRefreshEvent?.Invoke();
        return await ListT2IParams(session);
    }

    /// <summary>API route to select a model for loading.</summary>
    public static async Task<JObject> SelectModel(Session session, string model, string backendId = null)
    {
        return (await API.RunWebsocketHandlerCallDirect(SelectModelInternal, session, (model, backendId)))[0];
    }

    /// <summary>API route to select a model for loading, as a websocket with live status updates.</summary>
    public static async Task<JObject> SelectModelWS(WebSocket socket, Session session, string model)
    {
        await API.RunWebsocketHandlerCallWS(SelectModelInternal, session, (model, (string)null), socket);
        await socket.SendJson(BasicAPIFeatures.GetCurrentStatusRaw(session), API.WebsocketTimeout);
        return null;
    }

    /// <summary>Internal handler of the stable-diffusion model-load API route.</summary>
    public static async Task SelectModelInternal(Session session, (string, string) data, Action<JObject> output, bool isWS)
    {
        (string model, string backendId) = data;
        if (!session.User.Restrictions.CanChangeModels)
        {
            output(new JObject() { ["error"] = "You are not allowed to change models." });
            return;
        }
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (allowed != null && !allowed.IsMatch(model) || !Program.MainSDModels.Models.TryGetValue(model, out T2IModel actualModel))
        {
            output(new JObject() { ["error"] = "Model not found." });
            return;
        }
        using Session.GenClaim claim = session.Claim(0, Program.Backends.T2IBackends.Count, 0, 0);
        if (isWS)
        {
            output(BasicAPIFeatures.GetCurrentStatusRaw(session));
        }
        if (!(await Program.Backends.LoadModelOnAll(actualModel, backendId is null ? null : (b => $"{b.ID}" == backendId))))
        {
            output(new JObject() { ["error"] = "Model failed to load." });
            return;
        }
        output(new JObject() { ["success"] = true });
    }

    /// <summary>API route to modify the metadata of a model.</summary>
    public static async Task<JObject> EditModelMetadata(Session session, string model, string title, string author, string type, string description,
        int standard_width, int standard_height, string preview_image, string usage_hint, string date, string license, string trigger_phrase, string tags, string subtype = "Stable-Diffusion")
    {
        if (!Program.T2IModelSets.TryGetValue(subtype, out T2IModelHandler handler))
        {
            return new JObject() { ["error"] = "Invalid sub-type." };
        }
        if (!session.User.Restrictions.CanChangeModels)
        {
            return new JObject() { ["error"] = "You are not allowed to change models." };
        }
        // TODO: model-metadata-edit permission check
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (allowed != null && !allowed.IsMatch(model) || !handler.Models.TryGetValue(model, out T2IModel actualModel))
        {
            return new JObject() { ["error"] = "Model not found." };
        }
        lock (handler.ModificationLock)
        {
            actualModel.Title = string.IsNullOrWhiteSpace(title) ? null : title;
            actualModel.Description = description;
            if (!string.IsNullOrWhiteSpace(type))
            {
                actualModel.ModelClass = handler.ClassSorter.ModelClasses.GetValueOrDefault(type);
            }
            if (standard_width > 0)
            {
                actualModel.StandardWidth = standard_width;
            }
            if (standard_height > 0)
            {
                actualModel.StandardHeight = standard_height;
            }
            actualModel.Metadata ??= new();
            if (!string.IsNullOrWhiteSpace(preview_image))
            {
                actualModel.PreviewImage = preview_image;
                actualModel.Metadata.PreviewImage = preview_image;
            }
            actualModel.Metadata.Author = string.IsNullOrWhiteSpace(author) ? null : author;
            actualModel.Metadata.UsageHint = string.IsNullOrWhiteSpace(usage_hint) ? null : usage_hint;
            actualModel.Metadata.Date = string.IsNullOrWhiteSpace(date) ? null : date;
            actualModel.Metadata.License = string.IsNullOrWhiteSpace(license) ? null : license;
            actualModel.Metadata.TriggerPhrase = string.IsNullOrWhiteSpace(trigger_phrase) ? null : trigger_phrase;
            actualModel.Metadata.Tags = string.IsNullOrWhiteSpace(tags) ? null : tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }
        handler.ResetMetadataFrom(actualModel);
        _ = Task.Run(() => handler.ApplyNewMetadataDirectly(actualModel));
        return new JObject() { ["success"] = true };
    }

    /// <summary>API route to get a list of parameter types.</summary>
    public static async Task<JObject> ListT2IParams(Session session)
    {
        JObject modelData = new();
        foreach (T2IModelHandler handler in Program.T2IModelSets.Values)
        {
            modelData[handler.ModelType] = new JArray(handler.ListModelNamesFor(session).Order().ToArray());
        }
        return new JObject()
        {
            ["list"] = new JArray(T2IParamTypes.Types.Values.Select(v => v.ToNet(session)).ToList()),
            ["models"] = modelData,
            ["param_edits"] = string.IsNullOrWhiteSpace(session.User.Data.RawParamEdits) ? null : JObject.Parse(session.User.Data.RawParamEdits)
        };
    }
}
