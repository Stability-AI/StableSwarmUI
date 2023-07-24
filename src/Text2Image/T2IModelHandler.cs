using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using LiteDB;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;
using System.IO;
using System.Text.RegularExpressions;

namespace StableSwarmUI.Text2Image;

/// <summary>Central manager for Text2Image models.</summary>
public class T2IModelHandler
{
    /// <summary>All models known to this handler.</summary>
    public ConcurrentDictionary<string, T2IModel> Models = new();

    /// <summary>Helper to sort model classes.</summary>
    public T2IModelClassSorter ClassSorter = new();

    /// <summary>Lock used when modifying the model list.</summary>
    public LockObject ModificationLock = new();

    /// <summary>If true, the engine is shutting down.</summary>
    public bool IsShutdown = false;

    /// <summary>Internal model metadata cache data (per folder).</summary>
    public Dictionary<string, (LiteDatabase, ILiteCollection<ModelMetadataStore>)> ModelMetadataCachePerFolder = new();

    /// <summary>Lock for metadata processing.</summary>
    public LockObject MetadataLock = new();

    /// <summary>Helper, data store for model metadata.</summary>
    public class ModelMetadataStore
    {
        [BsonId]
        public string ModelName { get; set; }

        public long ModelFileVersion { get; set; }

        public string ModelClassType { get; set; }

        public string ModelMetadataRaw { get; set; }

        public string Title { get; set; }

        public string Author { get; set; }

        public string Description { get; set; }

        public string PreviewImage { get; set; }

        public int StandardWidth { get; set; }

        public int StandardHeight { get; set; }

        public string License { get; set; }

        public string UsageHint { get; set; }

        public string TriggerPhrase { get; set; }

        public string[] Tags { get; set; }

        public string MergedFrom { get; set; }

        public string Date { get; set; }
    }

    public T2IModelHandler()
    {
        Program.ModelRefreshEvent += Refresh;
    }

    public void Shutdown()
    {
        if (IsShutdown)
        {
            return;
        }
        IsShutdown = true;
        lock (MetadataLock)
        {
            foreach ((LiteDatabase ldb, _) in ModelMetadataCachePerFolder.Values)
            {
                ldb.Dispose();
            }
            ModelMetadataCachePerFolder.Clear();
        }
    }

    public List<T2IModel> ListModelsFor(Session session)
    {
        if (IsShutdown)
        {
            return new();
        }
        string allowedStr = session.User.Restrictions.AllowedModels;
        if (allowedStr == ".*")
        {
            return Models.Values.ToList();
        }
        Regex allowed = new(allowedStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        return Models.Values.Where(m => allowed.IsMatch(m.Name)).ToList();
    }

    /// <summary>Refresh the model list.</summary>
    public void Refresh()
    {
        if (IsShutdown)
        {
            return;
        }
        lock (ModificationLock)
        {
            Models.Clear();
            AddAllFromFolder("");
        }
    }

    /// <summary>Get (or create) the metadata cache for a given model folder.</summary>
    public ILiteCollection<ModelMetadataStore> GetCacheForFolder(string folder)
    {
        lock (MetadataLock)
        {
            try
            {
                return ModelMetadataCachePerFolder.GetOrCreate(folder, () =>
                {
                    LiteDatabase ldb = new(folder + "/model_metadata.ldb");
                    return (ldb, ldb.GetCollection<ModelMetadataStore>("models"));
                }).Item2;
            }
            catch (UnauthorizedAccessException) { return null; }
            catch (IOException) { return null; }
        }
    }

    /// <summary>Updates the metadata cache database to the metadata assigned to this model object.</summary>
    public void ResetMetadataFrom(T2IModel model)
    {
        long modified = ((DateTimeOffset)File.GetLastWriteTimeUtc(model.RawFilePath)).ToUnixTimeMilliseconds();
        string folder = model.RawFilePath.Replace('\\', '/').BeforeAndAfterLast('/', out string fileName);
        ILiteCollection<ModelMetadataStore> cache = GetCacheForFolder(folder);
        if (cache is null)
        {
            return;
        }
        ModelMetadataStore metadata = model.Metadata ?? new()
        {
            ModelFileVersion = modified,
            ModelName = fileName,
            Title = model.Title,
            ModelClassType = model.ModelClass?.Name,
            StandardHeight = model.ModelClass.StandardHeight,
            StandardWidth = model.ModelClass.StandardWidth
        };
        lock (MetadataLock)
        {
            cache.Upsert(metadata);
        }
    }

    /// <summary>Force-load the metadata for a model.</summary>
    public void LoadMetadata(T2IModel model)
    {
        if (model is null)
        {
            Logs.Warning($"Tried to load metadata for a null model?:\n{Environment.StackTrace}");
            return;
        }
        if (model.ModelClass is not null || model.Title is not null)
        {
            Logs.Debug($"Not loading metadata for {model.Name} as it is already loaded.");
            return;
        }
        if (!model.Name.EndsWith(".safetensors"))
        {
            Logs.Debug($"Not loading metadata for {model.Name} as it is not safetensors.");
            return;
        }
        string folder = model.RawFilePath.Replace('\\', '/').BeforeAndAfterLast('/', out string fileName);
        long modified = ((DateTimeOffset)File.GetLastWriteTimeUtc(model.RawFilePath)).ToUnixTimeMilliseconds();
        ILiteCollection<ModelMetadataStore> cache = GetCacheForFolder(folder);
        if (cache is null)
        {
            return;
        }
        ModelMetadataStore metadata;
        lock (MetadataLock)
        {
            metadata = cache.FindById(fileName);
        }
        if (metadata is null || metadata.ModelFileVersion != modified)
        {
            string header = T2IModel.GetSafetensorsHeaderFrom(model.RawFilePath);
            if (header is null)
            {
                Logs.Debug($"Not loading metadata for {model.Name} as it lacks a proper header.");
                return;
            }
            JObject headerData = header.ParseToJson();
            if (headerData is null)
            {
                Logs.Debug($"Not loading metadata for {model.Name} as the header is not JSON?");
                return;
            }
            JObject metaHeader = headerData["__metadata__"] as JObject;
            T2IModelClass clazz = ClassSorter.IdentifyClassFor(model, headerData);
            string img = metaHeader?.Value<string>("modelspec.thumbnail") ?? metaHeader?.Value<string>("preview_image");
            if (img is not null && !(img.StartsWith("/Output/") || img.StartsWith("data:image/")))
            {
                Logs.Warning($"Ignoring image in metadata of {model.Name} '{img}'");
                img = null;
            }
            int width, height;
            string res = metaHeader?.Value<string>("modelspec.resolution");
            if (res is not null)
            {
                width = int.Parse(res.BeforeAndAfter('x', out string h));
                height = int.Parse(h);
            }
            else
            {
                width = (metaHeader?.ContainsKey("standard_width") ?? false) ? metaHeader.Value<int>("standard_width") : (clazz?.StandardWidth ?? 0);
                height = (metaHeader?.ContainsKey("standard_height") ?? false) ? metaHeader.Value<int>("standard_height") : (clazz?.StandardHeight ?? 0);
            }
            metadata = new()
            {
                ModelFileVersion = modified,
                ModelName = fileName,
                ModelClassType = clazz?.Name,
                ModelMetadataRaw = header,
                Title = metaHeader?.Value<string>("modelspec.title") ?? metaHeader?.Value<string>("title") ?? fileName.BeforeLast('.'),
                Author = metaHeader?.Value<string>("modelspec.author") ?? metaHeader?.Value<string>("author"),
                Description = metaHeader?.Value<string>("modelspec.description") ?? metaHeader?.Value<string>("description"),
                PreviewImage = img,
                StandardWidth = width,
                StandardHeight = height,
                UsageHint = metaHeader?.Value<string>("modelspec.usage_hint"),
                MergedFrom = metaHeader?.Value<string>("modelspec.merged_from"),
                TriggerPhrase = metaHeader?.Value<string>("modelspec.trigger_phrase"),
                License = metaHeader?.Value<string>("modelspec.license"),
                Date = metaHeader?.Value<string>("modelspec.date"),
                Tags = metaHeader?.Value<string>("modelspec.tags")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            };
            lock (MetadataLock)
            {
                cache.Upsert(metadata);
            }
        }
        lock (ModificationLock)
        {
            model.Title = metadata.Title;
            model.ModelClass = ClassSorter.ModelClasses.GetValueOrDefault(metadata.ModelClassType ?? "");
            model.PreviewImage = string.IsNullOrWhiteSpace(metadata.PreviewImage) ? "imgs/model_placeholder.jpg" : metadata.PreviewImage;
            model.StandardWidth = metadata.StandardWidth;
            model.StandardHeight = metadata.StandardHeight;
            model.Metadata = metadata;
        }
    }

    /// <summary>Internal model adder route. Do not call.</summary>
    public void AddAllFromFolder(string folder)
    {
        if (IsShutdown)
        {
            return;
        }
        if (folder.StartsWith('.'))
        {
            return;
        }
        string prefix = folder == "" ? "" : $"{folder}/";
        string actualFolder = $"{Program.ServerSettings.Paths.SDModelFullPath}/{folder}";
        if (!Directory.Exists(actualFolder))
        {
            return;
        }
        foreach (string subfolder in Directory.EnumerateDirectories(actualFolder))
        {
            AddAllFromFolder($"{prefix}{subfolder.AfterLast('/')}");
        }
        foreach (string file in Directory.EnumerateFiles(actualFolder))
        {
            string fn = file.Replace('\\', '/').AfterLast('/');
            string fullFilename = $"{prefix}{fn}";
            if (fn.EndsWith(".safetensors"))
            {
                T2IModel model = new()
                {
                    Name = fullFilename,
                    RawFilePath = file,
                    Description = "(Metadata not yet loaded. Wait a minute, then refresh.)",
                    PreviewImage = "imgs/model_placeholder.jpg",
                };
                Models[fullFilename] = model;
                try
                {
                    LoadMetadata(model);
                }
                catch (Exception ex)
                {
                    if (Program.GlobalProgramCancel.IsCancellationRequested)
                    {
                        throw;
                    }
                    Logs.Warning($"Failed to load metadata for {fullFilename}:\n{ex}");
                }
            }
            else if (fn.EndsWith(".ckpt"))
            {
                Models[fullFilename] = new T2IModel()
                {
                    Name = fullFilename,
                    RawFilePath = file,
                    Description = "(None, use '.safetensors' to enable metadata descriptions)",
                    PreviewImage = "imgs/legacy_ckpt.jpg",
                };
            }
        }
    }
}
