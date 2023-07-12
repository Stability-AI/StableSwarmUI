using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.DataHolders;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text.RegularExpressions;

namespace StableSwarmUI.WebAPI;

/// <summary>Text-to-Image API routes</summary>
public static class T2IAPI
{
    public static void Register()
    {
        API.RegisterAPICall(GenerateText2Image);
        API.RegisterAPICall(GenerateText2ImageWS);
        API.RegisterAPICall(ListImages);
        API.RegisterAPICall(ListModels);
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
            if (obj.TryGetValue("image", out JToken image) && obj.TryGetValue("index", out JToken index))
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
        T2IParamInput user_input = new(session);
        try
        {
            foreach ((string key, JToken val) in rawInput)
            {
                if (T2IParamTypes.Types.ContainsKey(T2IParamTypes.CleanTypeName(key)))
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
        user_input.NormalizeSeeds();
        List<T2IEngine.ImageInBatch> imageSet = new();
        T2IEngine.ImageInBatch[] imageOut = null;
        List<Task> tasks = new();
        int max_degrees = session.User.Restrictions.MaxT2ISimultaneous;
        for (int i = 0; i < images && !claim.ShouldCancel; i++)
        {
            tasks.RemoveAll(t => t.IsCompleted);
            while (tasks.Count > max_degrees)
            {
                await Task.WhenAny(tasks);
            }
            if (claim.ShouldCancel)
            {
                break;
            }
            int index = i;
            T2IParamInput thisParams = user_input.Clone();
            thisParams.Set(T2IParamTypes.Seed, thisParams.Get(T2IParamTypes.Seed));
            tasks.Add(Task.Run(() => T2IEngine.CreateImageTask(thisParams, claim, output, setError, isWS, 2, // TODO: Max timespan configurable
                (outputs) =>
                {
                    foreach (Image image in outputs)
                    {
                        (string url, string filePath) = session.SaveImage(image, user_input);
                        if (url == "ERROR")
                        {
                            setError($"Server failed to save images.");
                            return;
                        }
                        int index;
                        lock (imageSet)
                        {
                            index = imageSet.Count;
                            imageSet.Add(new(image, () =>
                            {
                                if (filePath is not null && File.Exists(filePath))
                                {
                                    File.Delete(filePath);
                                }
                                imageOut[index] = null;
                            }));
                        }
                        output(new JObject() { ["image"] = url, ["index"] = index, ["metadata"] = image.GetMetadata() });
                    }
                })));
        }
        while (tasks.Any())
        {
            await Task.WhenAny(tasks);
            tasks.RemoveAll(t => t.IsCompleted);
        }
        imageOut = imageSet.ToArray();
        T2IEngine.PostBatchEvent?.Invoke(new(user_input, imageOut));
        output(new JObject() { ["discard_indices"] = JArray.FromObject(imageOut.FindAllIndicesOf(i => i is null).ToArray()) });
    }

    public static HashSet<string> ImageExtensions = new() { "png", "jpg", "html" };

    /// <summary>API route to get a list of available history images.</summary>
    private static JObject GetListAPIInternal(Session session, string path, string root, HashSet<string> extensions, Func<string, bool> isAllowed, Func<string, string, JObject> valToObj)
    {
        (path, string consoleError, string userError) = WebServer.CheckFilePath(root, path);
        if (consoleError is not null)
        {
            Logs.Error(consoleError);
            return new JObject() { ["error"] = userError };
        }
        try
        {
            return new JObject()
            {
                ["folders"] = JToken.FromObject(Directory.EnumerateDirectories(path).Select(Path.GetFileName).Where(isAllowed).ToList()),
                ["files"] = JToken.FromObject(Directory.EnumerateFiles(path).Where(isAllowed).Where(f => extensions.Contains(f.AfterLast('.'))).Select(f => f.Replace('\\', '/')).Select(f => valToObj(f, f.AfterLast('/'))).ToList())
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
                return new JObject() { ["error"] = "Error reading file list." };
            }
        }
    }

    /// <summary>API route to get a list of available history images.</summary>
    public static async Task<JObject> ListImages(Session session, string path)
    {
        string root = $"{Environment.CurrentDirectory}/{Program.ServerSettings.Paths.OutputPath}/{session.User.UserID}";
        return GetListAPIInternal(session, path, root, ImageExtensions, f => true, (file, name) => new JObject()
        {
            ["src"] = name,
            ["metadata"] = ImageMetadataTracker.GetMetadataFor(file)
        });
    }

    public static HashSet<string> ModelExtensions = new() { "safetensors", "ckpt" };

    /// <summary>API route to get a list of available models.</summary>
    public static async Task<JObject> ListModels(Session session, string path)
    {
        if (path != "")
        {
            path += '/';
        }
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        List<T2IModel> matches = Program.T2IModels.Models.Values.Where(m => m.Name.StartsWith(path) && m.Name.Length > path.Length && (allowed is null || allowed.IsMatch(m.Name))).ToList();
        return new JObject()
        {
            ["folders"] = JToken.FromObject(matches.Where(m => m.Name[path.Length..].Contains('/')).Select(m => m.Name.BeforeLast('/').AfterLast('/')).Distinct().ToList()),
            ["files"] = JToken.FromObject(matches.Where(m => !m.Name[path.Length..].Contains('/')).Select(m => m.ToNetObject()).ToList())
        };
    }

    /// <summary>API route to get a list of currently loaded models.</summary>
    public static async Task<JObject> ListLoadedModels(Session session)
    {
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        List<T2IModel> matches = Program.T2IModels.Models.Values.Where(m => m.AnyBackendsHaveLoaded && (allowed is null || allowed.IsMatch(m.Name))).ToList();
        return new JObject()
        {
            ["models"] = JToken.FromObject(matches.Select(m => m.ToNetObject()).ToList())
        };
    }

    /// <summary>API route to trigger a reload of the model list.</summary>
    public static async Task<JObject> TriggerRefresh(Session session)
    {
        Program.ModelRefreshEvent?.Invoke();
        return await ListT2IParams(session);
    }

    /// <summary>API route to select a model for loading.</summary>
    public static async Task<JObject> SelectModel(Session session, string model)
    {
        return (await API.RunWebsocketHandlerCallDirect(SelectModelInternal, session, model))[0];
    }

    /// <summary>API route to select a model for loading, as a websocket with live status updates.</summary>
    public static async Task<JObject> SelectModelWS(WebSocket socket, Session session, string model)
    {
        await API.RunWebsocketHandlerCallWS(SelectModelInternal, session, model, socket);
        await socket.SendJson(BasicAPIFeatures.GetCurrentStatusRaw(session), API.WebsocketTimeout);
        return null;
    }

    /// <summary>Internal handler of the model-load API route.</summary>
    public static async Task SelectModelInternal(Session session, string model, Action<JObject> output, bool isWS)
    {
        if (!session.User.Restrictions.CanChangeModels)
        {
            output(new JObject() { ["error"] = "You are not allowed to change models." });
            return;
        }
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (allowed != null && !allowed.IsMatch(model) || !Program.T2IModels.Models.TryGetValue(model, out T2IModel actualModel))
        {
            output(new JObject() { ["error"] = "Model not found." });
            return;
        }
        using Session.GenClaim claim = session.Claim(0, Program.Backends.T2IBackends.Count, 0, 0);
        if (isWS)
        {
            output(BasicAPIFeatures.GetCurrentStatusRaw(session));
        }
        if (!(await Program.Backends.LoadModelOnAll(actualModel)))
        {
            output(new JObject() { ["error"] = "Model failed to load." });
            return;
        }
        output(new JObject() { ["success"] = true });
    }

    /// <summary>API route to modify the metadata of a model.</summary>
    public static async Task<JObject> EditModelMetadata(Session session, string model, string title, string author, string type, string description, int standard_width, int standard_height, string preview_image)
    {
        if (!session.User.Restrictions.CanChangeModels)
        {
            return new JObject() { ["error"] = "You are not allowed to change models." };
        }
        // TODO: model-metadata-edit permission check
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (allowed != null && !allowed.IsMatch(model) || !Program.T2IModels.Models.TryGetValue(model, out T2IModel actualModel))
        {
            return new JObject() { ["error"] = "Model not found." };
        }
        lock (Program.T2IModels.ModificationLock)
        {
            actualModel.Title = string.IsNullOrWhiteSpace(title) ? null : title;
            actualModel.Author = string.IsNullOrWhiteSpace(author) ? null : author;
            if (!string.IsNullOrWhiteSpace(type))
            {
                actualModel.ModelClass = Program.T2IModels.ClassSorter.ModelClasses.GetValueOrDefault(type);
            }
            actualModel.Description = description;
            if (standard_width > 0)
            {
                actualModel.StandardWidth = standard_width;
            }
            if (standard_height > 0)
            {
                actualModel.StandardHeight = standard_height;
            }
            if (!string.IsNullOrWhiteSpace(preview_image))
            {
                actualModel.PreviewImage = preview_image;
            }
        }
        Program.T2IModels.ResetMetadataFrom(actualModel);
        return new JObject() { ["success"] = true };
    }

    /// <summary>API route to get a list of parameter types.</summary>
    public static async Task<JObject> ListT2IParams(Session session)
    {
        return new JObject()
        {
            ["list"] = new JArray(T2IParamTypes.Types.Values.Select(v => v.ToNet(session)).ToList()),
            ["models"] = new JArray(Program.T2IModels.ListModelsFor(session).Select(m => m.Name).ToArray())
        };
    }
}
