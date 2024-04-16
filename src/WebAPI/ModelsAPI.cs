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

public static class ModelsAPI
{
    public static void Register()
    {
        API.RegisterAPICall(ListModels);
        API.RegisterAPICall(DescribeModel);
        API.RegisterAPICall(ListLoadedModels);
        API.RegisterAPICall(SelectModel);
        API.RegisterAPICall(SelectModelWS);
        API.RegisterAPICall(DeleteWildcard);
        API.RegisterAPICall(TestPromptFill);
        API.RegisterAPICall(EditWildcard);
        API.RegisterAPICall(EditModelMetadata);
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

    /// <summary>API route to describe a single model.</summary>
    public static async Task<JObject> DescribeModel(Session session, string modelName, string subtype = "Stable-Diffusion")
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

    /// <summary>API route to get a list of available models.</summary>
    public static async Task<JObject> ListModels(Session session, string path, int depth, string subtype = "Stable-Diffusion")
    {
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
        List<JObject> files = [];
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
                    files.Add(card.GetNetObject());
                }
            }
        }
        else
        {
            foreach (T2IModel possible in handler.Models.Values)
            {
                if (tryMatch(possible.Name))
                {
                    files.Add(possible.ToNetObject());
                }
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

    /// <summary>API route to delete a wildcard.</summary>
    public static async Task<JObject> DeleteWildcard(Session session, string card)
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

    /// <summary>API route to test how a prompt fills.</summary>
    public static async Task<JObject> TestPromptFill(Session session, string prompt)
    {
        T2IParamInput input = new(session);
        input.Set(T2IParamTypes.Seed, Random.Shared.Next(int.MaxValue));
        input.Set(T2IParamTypes.Prompt, prompt);
        input.Set(T2IParamTypes.NegativePrompt, "");
        input.PreparsePromptLikes();
        return new JObject() { ["result"] = input.Get(T2IParamTypes.Prompt) };
    }

    /// <summary>API route to modify a wildcard.</summary>
    public static async Task<JObject> EditWildcard(Session session, string card, string options, string preview_image)
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
            Image img = Image.FromDataString(preview_image);
            File.WriteAllBytes($"{WildcardsHelper.Folder}/{card}.jpg", img.ToMetadataJpg().ImageData);
            WildcardsHelper.WildcardFiles[card] = new WildcardsHelper.Wildcard() { Name = card };
        }
        return new JObject() { ["success"] = true };
    }

    /// <summary>API route to modify the metadata of a model.</summary>
    public static async Task<JObject> EditModelMetadata(Session session, string model, string title, string author, string type, string description,
        int standard_width, int standard_height, string preview_image, string usage_hint, string date, string license, string trigger_phrase, string tags, bool is_negative_embedding = false, string subtype = "Stable-Diffusion")
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
                actualModel.PreviewImage = preview_image;
                actualModel.Metadata.PreviewImage = preview_image;
            }
            actualModel.Metadata.Author = string.IsNullOrWhiteSpace(author) ? null : author;
            actualModel.Metadata.UsageHint = string.IsNullOrWhiteSpace(usage_hint) ? null : usage_hint;
            actualModel.Metadata.Date = string.IsNullOrWhiteSpace(date) ? null : date;
            actualModel.Metadata.License = string.IsNullOrWhiteSpace(license) ? null : license;
            actualModel.Metadata.TriggerPhrase = string.IsNullOrWhiteSpace(trigger_phrase) ? null : trigger_phrase;
            actualModel.Metadata.Tags = string.IsNullOrWhiteSpace(tags) ? null : tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            actualModel.Metadata.IsNegativeEmbedding = is_negative_embedding;
        }
        handler.ResetMetadataFrom(actualModel);
        _ = Utilities.RunCheckedTask(() => handler.ApplyNewMetadataDirectly(actualModel));
        return new JObject() { ["success"] = true };
    }
}
