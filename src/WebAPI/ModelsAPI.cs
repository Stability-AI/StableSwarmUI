using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.IO;
using System.Net.WebSockets;
using System.Text.RegularExpressions;

namespace StableSwarmUI.WebAPI;

[API.APIClass("API routes related to handling models (including loras, wildcards, etc).")]
public static class ModelsAPI
{
    public static void Register()
    {
        API.RegisterAPICall(ListModels);
        API.RegisterAPICall(DescribeModel);
        API.RegisterAPICall(ListLoadedModels);
        API.RegisterAPICall(SelectModel, true);
        API.RegisterAPICall(SelectModelWS, true);
        API.RegisterAPICall(DeleteWildcard, true);
        API.RegisterAPICall(TestPromptFill);
        API.RegisterAPICall(EditWildcard, true);
        API.RegisterAPICall(EditModelMetadata, true);
        API.RegisterAPICall(DoModelDownloadWS, true);
    }

    public static Dictionary<string, JObject> InternalExtraModels(string subtype)
    {
        SwarmSwarmBackend[] backends = Program.Backends.RunningBackendsOfType<SwarmSwarmBackend>().Where(b => b.RemoteModels is not null).ToArray();
        IEnumerable<Dictionary<string, JObject>> sets = backends.Select(b => b.RemoteModels.GetValueOrDefault(subtype)).Where(b => b is not null);
        if (sets.IsEmpty())
        {
            return [];
        }
        return sets.Aggregate((a, b) => a.Union(b).PairsToDictionary(false));
    }

    /// <summary>Placeholder model indicating the lack of a model.</summary>
    public static T2IModel NoneModel = new() { Name = "(None)", Description = "No model selected.", RawFilePath = "(ERROR_NONE_MODEL_USED_LITERALLY)" };

    [API.APIDescription("Returns a full description for a single model.",
        """
            "model":
            {
                "name": "namehere",
                "title": "titlehere",
                "author": "authorhere",
                "description": "descriptionhere",
                "preview_image": "data:image/jpg;base64,abc123",
                "loaded": false, // true if any backend has the model loaded currently
                "architecture": "archhere", // model class ID
                "class": "classhere", // user-friendly class name
                "compat_class": "compatclasshere", // compatibility class name
                "standard_width": 1024,
                "standard_height": 1024,
                "license": "licensehere",
                "date": "datehere",
                "usage_hint": "usagehinthere",
                "trigger_phrase": "triggerphrasehere",
                "merged_from": "mergedfromhere",
                "tags": ["tag1", "tag2"],
                "is_supported_model_format": true,
                "is_negative_embedding": false,
                "local": true // false means remote servers (Swarm-API-Backend) have this model, but this server does not
            }
        """
        )]
    public static async Task<JObject> DescribeModel(Session session,
        [API.APIParameter("Full filepath name of the model being requested.")] string modelName,
        [API.APIParameter("What model sub-type to use, can be eg `LoRA` or `Wildcards` or etc.")] string subtype = "Stable-Diffusion")
    {
        if (!Program.T2IModelSets.TryGetValue(subtype, out T2IModelHandler handler) && subtype != "Wildcards")
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
            if (subtype == "Wildcards")
            {
                WildcardsHelper.Wildcard card = WildcardsHelper.GetWildcard(modelName);
                if (card is not null)
                {
                    return card.GetNetObject();
                }
            }
            else if (subtype == "Stable-Diffusion" && modelName.ToLowerFast() == "(none)")
            {
                return new JObject() { ["model"] = NoneModel.ToNetObject() };
            }
            else if (handler.Models.TryGetValue(modelName + ".safetensors", out T2IModel model))
            {
                return new JObject() { ["model"] = model.ToNetObject() };
            }
            else if (handler.Models.TryGetValue(modelName, out model))
            {
                return new JObject() { ["model"] = model.ToNetObject() };
            }
            else if (InternalExtraModels(subtype).TryGetValue(modelName + ".safetensors", out JObject remoteModel))
            {
                return new JObject() { ["model"] = remoteModel };
            }
            else if (InternalExtraModels(subtype).TryGetValue(modelName, out remoteModel))
            {
                return new JObject() { ["model"] = remoteModel };
            }
        }
        Logs.Debug($"Request for {subtype} model {modelName} rejected as not found.");
        return new JObject() { ["error"] = "Model not found." };
    }

    public enum ModelHistorySortMode { Name, Title, DateCreated, DateModified }

    public record struct ModelListEntry(string Name, string Title, long TimeCreated, long TimeModified, JObject NetData);

    [API.APIDescription("Returns a list of models available on the server within a given folder, with their metadata.",
        """
            "folders": ["folder1", "folder2"],
            "files":
            [
                {
                    "name": "namehere",
                    // etc., see `DescribeModel` for the full model description
                }
            ]
        """
        )]
    public static async Task<JObject> ListModels(Session session,
        [API.APIParameter("What folder path to search within. Use empty string for root.")] string path,
        [API.APIParameter("Maximum depth (number of recursive folders) to search.")] int depth,
        [API.APIParameter("Model sub-type - `LoRA`, `Wildcards`, etc.")] string subtype = "Stable-Diffusion",
        [API.APIParameter("What to sort the list by - `Name`, `DateCreated`, or `DateModified.")] string sortBy = "Name",
        [API.APIParameter("If true, the sorting should be done in reverse.")] bool sortReverse = false)
    {
        if (!Enum.TryParse(sortBy, true, out ModelHistorySortMode sortMode))
        {
            return new JObject() { ["error"] = "Invalid sort mode." };
        }
        if (!Program.T2IModelSets.TryGetValue(subtype, out T2IModelHandler handler) && subtype != "Wildcards")
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
        HashSet<string> folders = [];
        List<ModelListEntry> files = [];
        HashSet<string> dedup = [];
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
        if (subtype == "Wildcards")
        {
            foreach (string file in WildcardsHelper.ListFiles)
            {
                if (tryMatch(file))
                {
                    WildcardsHelper.Wildcard card = WildcardsHelper.GetWildcard(file);
                    files.Add(new(card.Name, card.Name.AfterLast('/'), card.TimeCreated, card.TimeModified, card.GetNetObject()));
                }
            }
        }
        else
        {
            foreach (T2IModel possible in handler.Models.Values)
            {
                if (tryMatch(possible.Name))
                {
                    files.Add(new(possible.Name, possible.Title, possible.Metadata?.TimeCreated ?? long.MaxValue, possible.Metadata?.TimeModified ?? long.MaxValue, possible.ToNetObject()));
                }
            }
        }
        foreach ((string name, JObject possible) in InternalExtraModels(subtype))
        {
            if (tryMatch(name))
            {
                files.Add(new(name, name.AfterLast('/'), long.MaxValue, long.MaxValue, possible));
            }
        }
        if (sortMode == ModelHistorySortMode.Name)
        {
            files = [.. files.OrderBy(a => a.Name)];
        }
        else if (sortMode == ModelHistorySortMode.Title)
        {
            files = [.. files.OrderBy(a => a.Title).ThenBy(a => a.Name)];
        }
        else if (sortMode == ModelHistorySortMode.DateCreated)
        {
            files = [.. files.OrderByDescending(a => a.TimeCreated).ThenBy(a => a.Name)];
        }
        else if (sortMode == ModelHistorySortMode.DateModified)
        {
            files = [.. files.OrderByDescending(a => a.TimeModified).ThenBy(a => a.Name)];
        }
        if (sortReverse)
        {
            files.Reverse();
        }
        return new JObject()
        {
            ["folders"] = JArray.FromObject(folders.ToList()),
            ["files"] = JArray.FromObject(files.Select(f => f.NetData).ToList())
        };
    }

    [API.APIDescription("Returns a list of currently loaded Stable-Diffusion models (ie at least one backend has it loaded).",
        """
            "models":
            [
                {
                    "name": "namehere",
                    // see `DescribeModel` for the full model description
                }
            ]
            """
        )]
    public static async Task<JObject> ListLoadedModels(Session session)
    {
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        List<T2IModel> matches = Program.MainSDModels.Models.Values.Where(m => m.AnyBackendsHaveLoaded && (allowed is null || allowed.IsMatch(m.Name))).ToList();
        return new JObject()
        {
            ["models"] = JArray.FromObject(matches.Select(m => m.ToNetObject()).ToList())
        };
    }

    [API.APIDescription("Forcibly loads a model immediately on some or all backends.", "\"success\": true")]
    public static async Task<JObject> SelectModel(Session session,
        [API.APIParameter("The full filepath of the model to load.")] string model,
        [API.APIParameter("The ID of a backend to load the model on, or null to load on all.")] string backendId = null)
    {
        return (await API.RunWebsocketHandlerCallDirect(SelectModelInternal, session, (model, backendId)))[0];
    }

    [API.APIDescription("Forcibly loads a model immediately on some or all backends, with live status updates over websocket.", "\"success\": true")]
    public static async Task<JObject> SelectModelWS(WebSocket socket, Session session, string model)
    {
        await API.RunWebsocketHandlerCallWS(SelectModelInternal, session, (model, (string)null), socket);
        await socket.SendJson(BasicAPIFeatures.GetCurrentStatusRaw(session), API.WebsocketTimeout);
        return null;
    }

    public static bool TryGetRefusalForModel(Session session, string name, out JObject refusal)
    {
        if (!session.User.Restrictions.CanChangeModels)
        {
            refusal = new JObject() { ["error"] = "You are not allowed to change models." };
            return true;
        }
        // TODO: model-metadata-edit permission check
        string allowedStr = session.User.Restrictions.AllowedModels;
        Regex allowed = allowedStr == ".*" ? null : new Regex(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if ((allowed != null && !allowed.IsMatch(name)) || string.IsNullOrWhiteSpace(name))
        {
            Logs.Warning($"Rejected model access for model '{name}' from user {session.User.UserID}");
            refusal = new JObject() { ["error"] = "Model not found." };
            return true;
        }
        refusal = null;
        return false;
    }

    /// <summary>Internal handler of the stable-diffusion model-load API route.</summary>
    public static async Task SelectModelInternal(Session session, (string, string) data, Action<JObject> output, bool isWS)
    {
        (string model, string backendId) = data;
        Logs.Verbose($"API request to select model '{model}' on backend '{backendId}' from user '{session.User.UserID}'");
        if (TryGetRefusalForModel(session, model, out JObject refusal))
        {
            Logs.Verbose("SelectModel refused generically");
            output(refusal);
            return;
        }
        if (!Program.MainSDModels.Models.TryGetValue(model + ".safetensors", out T2IModel actualModel) && !Program.MainSDModels.Models.TryGetValue(model, out actualModel))
        {
            Logs.Verbose("SelectModel refused due to unrecognized model");
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
            Logs.Verbose("SelectModel refused due to LoadModel returning false");
            output(new JObject() { ["error"] = "Model failed to load." });
            return;
        }
        Logs.Verbose("SelectModel succeeded");
        output(new JObject() { ["success"] = true });
    }

    [API.APIDescription("Deletes a wildcard file.", "\"success\": true")]
    public static async Task<JObject> DeleteWildcard(Session session,
        [API.APIParameter("Exact filepath name of the wildcard.")] string card)
    {
        card = Utilities.StrictFilenameClean(card);
        if (TryGetRefusalForModel(session, card, out JObject refusal))
        {
            return refusal;
        }
        if (!File.Exists($"{WildcardsHelper.Folder}/{card}.txt"))
        {
            return new JObject() { ["error"] = "Model not found." };
        }
        File.Delete($"{WildcardsHelper.Folder}/{card}.txt");
        if (File.Exists($"{WildcardsHelper.Folder}/{card}.jpg"))
        {
            File.Delete($"{WildcardsHelper.Folder}/{card}.jpg");
        }
        return new JObject() { ["success"] = true };
    }

    [API.APIDescription("Tests how a prompt fills. Useful for testing wildcards, `<random:...`, etc.",
        """
            "result": "your filled prompt"
        """)]
    public static async Task<JObject> TestPromptFill(Session session,
        [API.APIParameter("The prompt to fill.")] string prompt)
    {
        T2IParamInput input = new(session);
        input.Set(T2IParamTypes.Seed, Random.Shared.Next(int.MaxValue));
        input.Set(T2IParamTypes.Prompt, prompt);
        input.Set(T2IParamTypes.NegativePrompt, "");
        input.PreparsePromptLikes();
        return new JObject() { ["result"] = input.Get(T2IParamTypes.Prompt) };
    }

    [API.APIDescription("Edits a wildcard file.", "\"success\": true")]
    public static async Task<JObject> EditWildcard(Session session,
        [API.APIParameter("Exact filepath name of the wildcard.")] string card,
        [API.APIParameter("Newline-separated string listing of wildcard options.")] string options,
        [API.APIParameter("Image-data-string of a preview, or null to not change.")] string preview_image = null,
        [API.APIParameter("Optional raw text of metadata to inject to the preview image.")] string preview_image_metadata = null)
    {
        card = Utilities.StrictFilenameClean(card);
        if (TryGetRefusalForModel(session, card, out JObject refusal))
        {
            return refusal;
        }
        string path = $"{WildcardsHelper.Folder}/{card}.txt";
        string folder = Path.GetDirectoryName(path);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(path, StringConversionHelper.UTF8Encoding.GetBytes(options));
        if (!string.IsNullOrWhiteSpace(preview_image))
        {
            Image img = Image.FromDataString(preview_image).ToMetadataJpg(preview_image_metadata);
            File.WriteAllBytes($"{WildcardsHelper.Folder}/{card}.jpg", img.ImageData);
            WildcardsHelper.WildcardFiles[card] = new WildcardsHelper.Wildcard() { Name = card };
        }
        return new JObject() { ["success"] = true };
    }

    [API.APIDescription("Modifies the metadata of a model. Returns before the file update is necessarily saved.", "\"success\": true")]
    public static async Task<JObject> EditModelMetadata(Session session,
        [API.APIParameter("Exact filepath name of the model.")] string model,
        [API.APIParameter("New model `title` metadata value.")] string title,
        [API.APIParameter("New model `author` metadata value.")] string author,
        [API.APIParameter("New model `description` metadata value (architecture ID).")] string type,
        [API.APIParameter("New model `description` metadata value.")] string description,
        [API.APIParameter("New model `standard_width` metadata value.")] int standard_width,
        [API.APIParameter("New model `standard_height` metadata value.")] int standard_height,
        [API.APIParameter("New model `usage_hint` metadata value.")] string usage_hint,
        [API.APIParameter("New model `date` metadata value.")] string date,
        [API.APIParameter("New model `license` metadata value.")] string license,
        [API.APIParameter("New model `trigger_phrase` metadata value.")] string trigger_phrase,
        [API.APIParameter("New model `prediction_type` metadata value.")] string prediction_type,
        [API.APIParameter("New model `tags` metadata value (comma-separated list).")] string tags,
        [API.APIParameter("New model `preview_image` metadata value (image-data-string format, or null to not change).")] string preview_image = null,
        [API.APIParameter("Optional raw text of metadata to inject to the preview image.")] string preview_image_metadata = null,
        [API.APIParameter("New model `is_negative_embedding` metadata value.")] bool is_negative_embedding = false,
        [API.APIParameter("The model's sub-type, eg `Stable-Diffusion`, `LoRA`, etc.")] string subtype = "Stable-Diffusion")
    {
        if (!Program.T2IModelSets.TryGetValue(subtype, out T2IModelHandler handler))
        {
            return new JObject() { ["error"] = "Invalid sub-type." };
        }
        if (TryGetRefusalForModel(session, model, out JObject refusal))
        {
            return refusal;
        }
        if (!handler.Models.TryGetValue(model, out T2IModel actualModel))
        {
            return new JObject() { ["error"] = "Model not found." };
        }
        lock (handler.ModificationLock)
        {
            actualModel.Title = string.IsNullOrWhiteSpace(title) ? null : title;
            actualModel.Description = description;
            if (!string.IsNullOrWhiteSpace(type))
            {
                actualModel.ModelClass = T2IModelClassSorter.ModelClasses.GetValueOrDefault(type);
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
                Image img = Image.FromDataString(preview_image).ToMetadataJpg(preview_image_metadata);
                actualModel.PreviewImage = img.AsDataString();
                actualModel.Metadata.PreviewImage = actualModel.PreviewImage;
            }
            actualModel.Metadata.Author = string.IsNullOrWhiteSpace(author) ? null : author;
            actualModel.Metadata.UsageHint = string.IsNullOrWhiteSpace(usage_hint) ? null : usage_hint;
            actualModel.Metadata.Date = string.IsNullOrWhiteSpace(date) ? null : date;
            actualModel.Metadata.License = string.IsNullOrWhiteSpace(license) ? null : license;
            actualModel.Metadata.TriggerPhrase = string.IsNullOrWhiteSpace(trigger_phrase) ? null : trigger_phrase;
            actualModel.Metadata.Tags = string.IsNullOrWhiteSpace(tags) ? null : tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            actualModel.Metadata.IsNegativeEmbedding = is_negative_embedding;
            actualModel.Metadata.PredictionType = string.IsNullOrWhiteSpace(prediction_type) ? null : prediction_type;
        }
        handler.ResetMetadataFrom(actualModel);
        _ = Utilities.RunCheckedTask(() => handler.ApplyNewMetadataDirectly(actualModel));
        return new JObject() { ["success"] = true };
    }


    [API.APIDescription("Downloads a model to the server, with websocket progress updates.\nNote that this does not trigger a model refresh itself, you must do that after a 'success' reply.", "")]
    public static async Task<JObject> DoModelDownloadWS(Session session, WebSocket ws,
        [API.APIParameter("The URL to download a model from.")] string url,
        [API.APIParameter("The model's sub-type, eg `Stable-Diffusion`, `LoRA`, etc.")] string type,
        [API.APIParameter("The filename to use for the model.")] string name,
        [API.APIParameter("Optional raw text of JSON metadata to inject to the model.")] string metadata = null)
    {
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            return new JObject() { ["error"] = "Invalid URL." };
        }
        name = Utilities.StrictFilenameClean(name);
        if (TryGetRefusalForModel(session, name, out JObject refusal))
        {
            return refusal;
        }
        if (!Program.T2IModelSets.TryGetValue(type, out T2IModelHandler handler))
        {
            return new JObject() { ["error"] = "Invalid type." };
        }
        try
        {
            string outPath = $"{handler.FolderPaths[0]}/{name}.safetensors";
            if (File.Exists(outPath))
            {
                return new JObject() { ["error"] = "Model at that save path already exists." };
            }
            string tempPath = $"{handler.FolderPaths[0]}/{name}.download.tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            await Utilities.DownloadFile(url, tempPath, async (progress, total) =>
            {
                await ws.SendJson(new JObject()
                {
                    ["current_percent"] = progress / (double)total,
                    ["overall_percent"] = 0.2
                }, API.WebsocketTimeout);
            });
            File.Move(tempPath, outPath);
            if (!string.IsNullOrWhiteSpace(metadata))
            {
                File.WriteAllText($"{handler.FolderPaths[0]}/{name}.json", metadata);
            }
            await ws.SendJson(new JObject() { ["success"] = true }, API.WebsocketTimeout);
        }
        catch (Exception ex)
        {
            if (ex is InvalidOperationException || ex is InvalidDataException)
            {
                Logs.Warning($"Failed to download the model due to: {ex.Message}");
                await ws.SendJson(new JObject() { ["error"] = ex.Message }, API.WebsocketTimeout);
                return null;
            }
            Logs.Warning($"Failed to download the model due to internal exception: {ex}");
            await ws.SendJson(new JObject() { ["error"] = "Failed to download the model due to internal exception." }, API.WebsocketTimeout);
        }
        return null;
    }
}
