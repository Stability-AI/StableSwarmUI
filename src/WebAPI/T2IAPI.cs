using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableUI.Accounts;
using StableUI.Backends;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Text2Image;
using StableUI.Utils;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text.RegularExpressions;

namespace StableUI.WebAPI;

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
        API.RegisterAPICall(RefreshModels);
        API.RegisterAPICall(SelectModel);
        API.RegisterAPICall(ListT2IParams);
    }

    /// <summary>API route to generate images with WebSocket updates.</summary>
    public static async Task<JObject> GenerateText2ImageWS(WebSocket socket, Session session, int images, JObject rawInput)
    {
        await foreach ((string img, JObject err) in GenT2I_Internal(session, images, rawInput))
        {
            if (img is not null)
            {
                await socket.SendJson(new JObject() { ["image"] = img }, TimeSpan.FromMinutes(1)); // TODO: Configurable timeout
            }
            if (err is not null)
            {
                await socket.SendJson(err, TimeSpan.FromMinutes(1));
                break;
            }
        }
        return null;
    }

    /// <summary>API route to generate images directly as HTTP.</summary>
    public static async Task<JObject> GenerateText2Image(Session session, int images, JObject rawInput)
    {
        List<string> outputs = new();
        await foreach ((string img, JObject err) in GenT2I_Internal(session, images, rawInput))
        {
            if (img is not null)
            {
                outputs.Add(img);
            }
            if (err is not null)
            {
                return err;
            }
        }
        return new JObject() { ["images"] = JToken.FromObject(outputs) };
    }

    /// <summary>Internal route for generating images.</summary>
    public static async IAsyncEnumerable<(string, JObject)> GenT2I_Internal(Session session, int images, JObject rawInput)
    {
        using Session.GenClaim claim = session.Claim(images);
        T2IParams user_input = new(session);
        string err = null;
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
        catch (InvalidDataException ex)
        {
            err = ex.Message; // NOTE: Weird C# limit, can't 'yield return' inside a 'catch'.
        }
        if (err is not null)
        {
            yield return (null, new JObject() { ["error"] = err });
            yield break;
        }
        ConcurrentQueue<string> allOutputs = new();
        if (user_input.Seed == -1)
        {
            user_input.Seed = Random.Shared.Next(int.MaxValue);
        }
        JObject errorOut = null;
        List<Task> tasks = new();
        int max_degrees = session.User.Settings.MaxT2ISimultaneous;
        for (int i = 0; i < images; i++)
        {
            if (claim.ShouldCancel)
            {
                break;
            }
            tasks.RemoveAll(t => t.IsCompleted);
            if (tasks.Count > max_degrees)
            {
                await Task.WhenAny(tasks);
            }
            if (Volatile.Read(ref errorOut) is not null)
            {
                yield return (null, errorOut);
                yield break;
            }
            while (allOutputs.TryDequeue(out string output))
            {
                yield return (output, null);
            }
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                if (claim.ShouldCancel)
                {
                    return;
                }
                T2IBackendAccess backend;
                try
                {
                    backend = Program.Backends.GetNextT2IBackend(TimeSpan.FromMinutes(2), user_input.Model, user_input.BackendMatcher, claim.InterruptToken); // TODO: Max timespan configurable
                }
                catch (InvalidOperationException ex)
                {
                    Volatile.Write(ref errorOut, new JObject() { ["error"] = $"Invalid operation: {ex.Message}" });
                    return;
                }
                catch (TimeoutException)
                {
                    Volatile.Write(ref errorOut, new JObject() { ["error"] = "Timeout! All backends are occupied with other tasks." });
                    return;
                }
                if (claim.ShouldCancel)
                {
                    backend.Dispose();
                    return;
                }
                try
                {
                    using (backend)
                    {
                        if (Volatile.Read(ref errorOut) is not null || claim.ShouldCancel)
                        {
                            return;
                        }
                        T2IParams thisParams = user_input.Clone();
                        thisParams.Seed += index;
                        Image[] outputs = await backend.Backend.Generate(thisParams);
                        foreach (Image image in outputs)
                        {
                            string url = session.SaveImage(image, thisParams);
                            if (url == "ERROR")
                            {
                                Volatile.Write(ref errorOut, new JObject() { ["error"] = $"Server failed to save images." });
                                return;
                            }
                            claim.Complete(1);
                            allOutputs.Enqueue(url);
                        }
                    }
                }
                catch (InvalidDataException ex)
                {
                    Volatile.Write(ref errorOut, new JObject() { ["error"] = $"Invalid data: {ex.Message}" });
                    return;
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logs.Error($"Internal error processing T2I request: {ex}");
                    Volatile.Write(ref errorOut, new JObject() { ["error"] = "Something went wrong while generating images." });
                    return;
                }
            }));
        }
        while (tasks.Any())
        {
            await Task.WhenAny(tasks);
            tasks.RemoveAll(t => t.IsCompleted);
            while (allOutputs.TryDequeue(out string output))
            {
                yield return (output, null);
            }
            if (claim.ShouldCancel)
            {
                break;
            }
        }
        errorOut = Volatile.Read(ref errorOut);
        if (errorOut is not null)
        {
            yield return (null, errorOut);
            yield break;
        }
    }

    public static HashSet<string> ImageExtensions = new() { "png", "jpg" };

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
        string root = $"{Environment.CurrentDirectory}/{Program.ServerSettings.OutputPath}/{session.User.UserID}";
        return GetListAPIInternal(session, path, root, ImageExtensions, f => true, (file, name) => new JObject() { ["src"] = name, ["batch_id"] = 0 });
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
    public static async Task<JObject> RefreshModels(Session session)
    {
        Program.T2IModels.Refresh();
        return new JObject() { ["success"] = true };
    }

    /// <summary>API route to select a model for loading.</summary>
    public static async Task<JObject> SelectModel(Session session, string model)
    {
        if (!session.User.Restrictions.CanChangeModels)
        {
            return new JObject() { ["error"] = "You are not allowed to change models." };
        }
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (allowed != null && !allowed.IsMatch(model) || !Program.T2IModels.Models.TryGetValue(model, out T2IModel actualModel))
        {
            return new JObject() { ["error"] = "Model not found." };
        }
        if (!(await Program.Backends.LoadModelOnAll(actualModel)))
        {
            return new JObject() { ["error"] = "Model failed to load." };
        }
        return new JObject() { ["success"] = true };
    }

    /// <summary>API route to get a list of parameter types.</summary>
    public static async Task<JObject> ListT2IParams(Session session)
    {
        return new JObject()
        {
            ["list"] = JToken.FromObject(T2IParamTypes.Types.Values.Select(v => v.ToNet(session)).ToList())
        };
    }
}
