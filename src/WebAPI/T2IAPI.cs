using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using StableSwarmUI.Accounts;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using Image = StableSwarmUI.Utils.Image;
using ISImage = SixLabors.ImageSharp.Image;
using ISImageRGBA = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace StableSwarmUI.WebAPI;

[API.APIClass("API routes for actual text-to-image processing and directly related features.")]
public static class T2IAPI
{
    public static void Register()
    {
        // TODO: Some of these shouldn't be here?
        API.RegisterAPICall(GenerateText2Image, true);
        API.RegisterAPICall(GenerateText2ImageWS, true);
        API.RegisterAPICall(ListImages);
        API.RegisterAPICall(ToggleImageStarred, true);
        API.RegisterAPICall(OpenImageFolder, true);
        API.RegisterAPICall(DeleteImage, true);
        API.RegisterAPICall(ListT2IParams);
        API.RegisterAPICall(TriggerRefresh);
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
        Dictionary<int, string> imageOutputs = [];
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

    public static HashSet<string> AlwaysTopKeys = [];

    /// <summary>Helper util to take a user-supplied JSON object of parameter data and turn it into a valid T2I request object.</summary>
    public static T2IParamInput RequestToParams(Session session, JObject rawInput)
    {
        T2IParamInput user_input = new(session);
        List<string> keys = rawInput.Properties().Select(p => p.Name).ToList();
        keys = keys.Where(AlwaysTopKeys.Contains).Concat(keys.Where(k => !AlwaysTopKeys.Contains(k))).ToList();
        foreach (string key in keys)
        {
            if (key == "session_id" || key == "presets")
            {
                // Skip
            }
            else if (T2IParamTypes.TryGetType(key, out _, user_input))
            {
                T2IParamTypes.ApplyParameter(key, rawInput[key].ToString(), user_input);
            }
            else
            {
                Logs.Warning($"T2I image request from user {session.User.UserID} had request parameter '{key}', but that parameter is unrecognized, skipping...");
            }
        }
        if (rawInput.TryGetValue("presets", out JToken presets))
        {
            foreach (JToken presetName in presets.Values())
            {
                T2IPreset presetObj = session.User.GetPreset(presetName.ToString());
                if (presetObj is null)
                {
                    Logs.Warning($"User {session.User.UserID} tried to use preset '{presetName}', but it does not exist!");
                    continue;
                }
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
        long timeStart = Environment.TickCount64;
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
        Logs.Info($"User {session.User.UserID} requested {images} image{(images == 1 ? "": "s")} with model '{user_input.Get(T2IParamTypes.Model)?.Name}'...");
        if (Logs.MinimumLevel <= Logs.LogLevel.Verbose)
        {
            Logs.Verbose($"User {session.User.UserID} above image request had parameters: {user_input}");
        }
        List<T2IEngine.ImageOutput> imageSet = [];
        List<Task> tasks = [];
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
        List<int> discard = [];
        int numExtra = 0, numNonReal = 0;
        int batchSizeExpected = user_input.Get(T2IParamTypes.BatchSize, 1);
        void saveImage(T2IEngine.ImageOutput image, int actualIndex, T2IParamInput thisParams, string metadata)
        {
            (string url, string filePath) = thisParams.Get(T2IParamTypes.DoNotSave, false) ? (session.GetImageB64(image.Img), null) : session.SaveImage(image.Img, actualIndex, thisParams, metadata);
            if (url == "ERROR")
            {
                setError($"Server failed to save an image.");
                return;
            }
            image.RefuseImage = () =>
            {
                if (filePath is not null && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                discard.Add(actualIndex);
                lock (imageSet)
                {
                    imageSet.Remove(image);
                }
            };
            lock (imageSet)
            {
                imageSet.Add(image);
            }
            output(new JObject() { ["image"] = url, ["batch_index"] = $"{actualIndex}", ["metadata"] = string.IsNullOrWhiteSpace(metadata) ? null : metadata });
        }
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
            if (!thisParams.Get(T2IParamTypes.NoSeedIncrement, false))
            {
                if (thisParams.TryGet(T2IParamTypes.VariationSeed, out long varSeed) && thisParams.Get(T2IParamTypes.VariationSeedStrength) > 0)
                {
                    thisParams.Set(T2IParamTypes.VariationSeed, varSeed + imageIndex);
                }
                else
                {
                    thisParams.Set(T2IParamTypes.Seed, thisParams.Get(T2IParamTypes.Seed) + imageIndex);
                }
            }
            int numCalls = 0;
            tasks.Add(Task.Run(() => T2IEngine.CreateImageTask(thisParams, $"{imageIndex}", claim, output, setError, isWS, Program.ServerSettings.Backends.PerRequestTimeoutMinutes,
                (image, metadata) =>
                {
                    int actualIndex = imageIndex + numCalls;
                    if (image.IsReal)
                    {
                        numCalls++;
                        if (numCalls > batchSizeExpected)
                        {
                            actualIndex = images * batchSizeExpected + Interlocked.Increment(ref numExtra);
                        }
                    }
                    else
                    {
                        actualIndex = -10 - Interlocked.Increment(ref numNonReal);
                    }
                    saveImage(image, actualIndex, thisParams, metadata);
                })));
            if (Program.Backends.QueuedRequests < Program.ServerSettings.Backends.MaxRequestsForcedOrder)
            {
                Task.Delay(20).Wait(); // Tiny few-ms delay to encourage tasks retaining order.
            }
        }
        while (tasks.Any())
        {
            await Task.WhenAny(tasks);
            removeDoneTasks();
        }
        long finalTime = Environment.TickCount64;
        T2IEngine.ImageOutput[] griddables = imageSet.Where(i => i.IsReal).ToArray();
        if (griddables.Length < session.User.Settings.MaxImagesInMiniGrid && griddables.Length > 1 && griddables.All(i => i.Img.Type == Image.ImageType.IMAGE))
        {
            ISImage[] imgs = griddables.Select(i => i.Img.ToIS).ToArray();
            int columns = (int)Math.Ceiling(Math.Sqrt(imgs.Length));
            int rows = columns;
            if (griddables.Length <= columns * (columns - 1))
            {
                rows--;
            }
            int widthPerImage = imgs.Max(i => i.Width);
            int heightPerImage = imgs.Max(i => i.Height);
            ISImageRGBA grid = new(widthPerImage * columns, heightPerImage * rows);
            grid.Mutate(m =>
            {
                for (int i = 0; i < imgs.Length; i++)
                {
                    int x = (i % columns) * widthPerImage, y = (i / columns) * heightPerImage;
                    m.DrawImage(imgs[i], new Point(x, y), 1);
                }
            });
            Image gridImg = new(grid);
            long genTime = Environment.TickCount64 - timeStart;
            user_input.ExtraMeta["generation_time"] = $"{genTime / 1000.0:0.00} total seconds (average {(finalTime - timeStart) / griddables.Length / 1000.0:0.00} seconds per image)";
            (gridImg, string metadata) = user_input.SourceSession.ApplyMetadata(gridImg, user_input, imgs.Length);
            T2IEngine.ImageOutput gridOutput = new() { Img = gridImg, GenTimeMS = genTime };
            saveImage(gridOutput, -1, user_input, metadata);
        }
        T2IEngine.PostBatchEvent?.Invoke(new(user_input, [.. griddables]));
        output(new JObject() { ["discard_indices"] = JToken.FromObject(discard) });
    }

    public static HashSet<string> ImageExtensions = ["png", "jpg", "html", "gif", "webm", "mp4", "webp", "mov"];

    public enum ImageHistorySortMode { Name, Date }

    // TODO: Configurable limit
    /// <summary>API route to get a list of available history images.</summary>
    private static JObject GetListAPIInternal(Session session, string path, string root, HashSet<string> extensions, Func<string, bool> isAllowed, int depth, ImageHistorySortMode sortBy, bool sortReverse)
    {
        int maxInHistory = session.User.Settings.MaxImagesInHistory;
        int maxScanned = session.User.Settings.MaxImagesScannedInHistory;
        Logs.Verbose($"User {session.User.UserID} wants to list images in '{path}', maxDepth={depth}, sortBy={sortBy}, reverse={sortReverse}, maxInHistory={maxInHistory}, maxScanned={maxScanned}");
        int limit = sortBy == ImageHistorySortMode.Name ? maxInHistory : Math.Max(maxInHistory, maxScanned);
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
            List<string> dirs = [];
            List<string> finalDirs = [];
            void addDirs(string dir, int subDepth)
            {
                if (dir != "")
                {
                    (subDepth == 0 ? finalDirs : dirs).Add(dir);
                }
                if (subDepth > 0)
                {
                    IEnumerable<string> subDirs = Directory.EnumerateDirectories(path + "/" + dir).Select(Path.GetFileName).OrderDescending();
                    if (sortReverse)
                    {
                        subDirs = subDirs.Reverse();
                    }
                    foreach (string subDir in subDirs)
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
            List<ImageHistoryHelper> files = [];
            bool starNoFolders = session.User.Settings.StarNoFolders;
            foreach (string folder in dirs.Append(""))
            {
                string prefix = folder == "" ? "" : folder + "/";
                List<string> subFiles = Directory.EnumerateFiles($"{path}/{prefix}").Take(limit).ToList();
                IEnumerable<string> newFileNames = subFiles.Where(isAllowed).Where(f => extensions.Contains(f.AfterLast('.'))).Select(f => f.Replace('\\', '/'));
                files.AddRange(newFileNames.Select(f => new ImageHistoryHelper(prefix + f.AfterLast('/'), ImageMetadataTracker.GetMetadataFor(f, root, starNoFolders))).Where(f => f.Metadata is not null));
                limit -= subFiles.Count;
                if (limit <= 0)
                {
                    break;
                }
            }
            if (sortBy == ImageHistorySortMode.Name)
            {
                files.Sort((a, b) => b.Name.CompareTo(a.Name));
            }
            else if (sortBy == ImageHistorySortMode.Date)
            {
                files.Sort((a, b) => b.Metadata.FileTime.CompareTo(a.Metadata.FileTime));
            }
            if (sortReverse)
            {
                files.Reverse();
            }
            return new JObject()
            {
                ["folders"] = JToken.FromObject(dirs.Union(finalDirs).ToList()),
                ["files"] = JToken.FromObject(files.Take(maxInHistory).Select(f => new JObject() { ["src"] = f.Name, ["metadata"] = f.Metadata.Metadata }).ToList())
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

    public record struct ImageHistoryHelper(string Name, ImageMetadataTracker.ImageMetadataEntry Metadata);

    /// <summary>API route to cause an image folder to open.</summary>
    public static async Task<JObject> OpenImageFolder(Session session, string path)
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
            Logs.Warning($"User {session.User.UserID} tried to open image path '{origPath}' which maps to '{path}', but cannot as the image does not exist.");
            return new JObject() { ["error"] = "That file does not exist, cannot open." };
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start("explorer.exe", $"/select,\"{Path.GetFullPath(path)}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", $"\"{Path.GetFullPath(path)}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", $"-R \"{Path.GetFullPath(path)}\"");
        }
        else
        {
            Logs.Warning("Cannot open image path on unrecognized OS type.");
            return new JObject() { ["error"] = "Cannot open image folder on this OS." };
        }
        return new JObject() { ["success"] = true };
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
    public static async Task<JObject> ListImages(Session session, string path, int depth, string sortBy = "Name", bool sortReverse = false)
    {
        if (!Enum.TryParse(sortBy, true, out ImageHistorySortMode sortMode))
        {
            return new JObject() { ["error"] = "Invalid sort mode." };
        }
        string root = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, session.User.OutputDirectory);
        return GetListAPIInternal(session, path, root, ImageExtensions, f => true, depth, sortMode, sortReverse);
    }

    /// <summary>API route to toggle whether an image is starred or not.</summary>
    public static async Task<JObject> ToggleImageStarred(Session session, string path)
    {
        path = path.Replace('\\', '/').Trim('/');
        if (path.StartsWith("Starred/"))
        {
            path = path["Starred/".Length..];
        }
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
            Logs.Warning($"User {session.User.UserID} tried to star image path '{origPath}' which maps to '{path}', but cannot as the image does not exist.");
            return new JObject() { ["error"] = "That file does not exist, cannot star." };
        }
        string starPath = $"Starred/{(session.User.Settings.StarNoFolders ? origPath.Replace("/", "") : origPath)}";
        (starPath, _, _) = WebServer.CheckFilePath(root, starPath);
        if (File.Exists(starPath))
        {
            File.Delete(starPath);
            ImageMetadataTracker.RemoveMetadataFor(path);
            ImageMetadataTracker.RemoveMetadataFor(starPath);
            return new JObject() { ["new_state"] = false };
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(starPath));
            File.Copy(path, starPath);
            ImageMetadataTracker.RemoveMetadataFor(path);
            ImageMetadataTracker.RemoveMetadataFor(starPath);
            return new JObject() { ["new_state"] = true };
        }
    }

    public static SemaphoreSlim RefreshSemaphore = new(1, 1);

    /// <summary>API route to trigger a reload of the model list.</summary>
    public static async Task<JObject> TriggerRefresh(Session session, bool strong = true)
    {
        Logs.Verbose($"User {session.User.UserID} triggered a {(strong ? "strong" : "weak")} data refresh");
        bool botherToRun = strong && RefreshSemaphore.CurrentCount > 0; // no need to run twice at once
        try
        {
            await RefreshSemaphore.WaitAsync(Program.GlobalProgramCancel);
            if (botherToRun)
            {
                Program.ModelRefreshEvent?.Invoke();
            }
        }
        finally
        {
            RefreshSemaphore.Release();
        }
        Logs.Debug($"Data refreshed!");
        return await ListT2IParams(session);
    }

    /// <summary>API route to get a list of parameter types.</summary>
    public static async Task<JObject> ListT2IParams(Session session)
    {
        JObject modelData = [];
        foreach (T2IModelHandler handler in Program.T2IModelSets.Values)
        {
            modelData[handler.ModelType] = new JArray(handler.ListModelNamesFor(session).Order().ToArray());
        }
        return new JObject()
        {
            ["list"] = new JArray(T2IParamTypes.Types.Values.Select(v => v.ToNet(session)).ToList()),
            ["models"] = modelData,
            ["wildcards"] = new JArray(WildcardsHelper.ListFiles),
            ["param_edits"] = string.IsNullOrWhiteSpace(session.User.Data.RawParamEdits) ? null : JObject.Parse(session.User.Data.RawParamEdits)
        };
    }
}
