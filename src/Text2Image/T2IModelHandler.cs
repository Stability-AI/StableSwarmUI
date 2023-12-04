using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using LiteDB;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;
using StableSwarmUI.WebAPI;
using System.IO;
using System.Security.Cryptography;
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

    /// <summary>The type of model this handler is tracking (eg Stable-Diffusion, LoRA, VAE, Embedding, ...).</summary>
    public string ModelType;

    /// <summary>The full folder path for relevant models.</summary>
    public string FolderPath;

    /// <summary>Helper, data store for model metadata.</summary>
    public class ModelMetadataStore
    {
        [BsonId]
        public string ModelName { get; set; }

        public long ModelFileVersion { get; set; }

        public string ModelClassType { get; set; }

        public string Title { get; set; }

        public string Author { get; set; }

        public string Description { get; set; }

        public string PreviewImage { get; set; }

        public int StandardWidth { get; set; }

        public int StandardHeight { get; set; }

        public bool IsNegativeEmbedding { get; set; }

        public string License { get; set; }

        public string UsageHint { get; set; }

        public string TriggerPhrase { get; set; }

        public string[] Tags { get; set; }

        public string MergedFrom { get; set; }

        public string Date { get; set; }

        public string Preprocesor { get; set; }
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
        Program.ModelRefreshEvent -= Refresh;
        lock (MetadataLock)
        {
            foreach ((LiteDatabase ldb, _) in ModelMetadataCachePerFolder.Values)
            {
                ldb.Dispose();
            }
            ModelMetadataCachePerFolder.Clear();
        }
    }


    /// <summary>Utility to destroy all stored metadata files.</summary>
    public void MassRemoveMetadata()
    {
        lock (MetadataLock)
        {
            foreach ((LiteDatabase ldb, _) in ModelMetadataCachePerFolder.Values)
            {
                try
                {
                    ldb.Dispose();
                }
                catch (Exception) { }
            }
            ModelMetadataCachePerFolder.Clear();
            static void ClearFolder(string folder)
            {
                if (File.Exists($"{folder}/model_metadata.ldb"))
                {
                    try
                    {
                        File.Delete($"{folder}/model_metadata.ldb");
                    }
                    catch (IOException) { }
                }
                foreach (string subFolder in Directory.GetDirectories(folder))
                {
                    ClearFolder(subFolder);
                }
            }
            ClearFolder(FolderPath);
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

    public List<string> ListModelNamesFor(Session session)
    {
        HashSet<string> list = ListModelsFor(session).Select(m => m.Name).ToHashSet();
        list.UnionWith(T2IAPI.InternalExtraModels(ModelType).Keys);
        return list.ToList();
    }

    /// <summary>Refresh the model list.</summary>
    public void Refresh()
    {
        if (IsShutdown)
        {
            return;
        }
        Directory.CreateDirectory(FolderPath);
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
        ModelMetadataStore metadata = model.Metadata ?? new();
        metadata.ModelFileVersion = modified;
        metadata.ModelName = fileName;
        metadata.Title = model.Title;
        metadata.Description = model.Description;
        metadata.ModelClassType = model.ModelClass?.ID;
        metadata.StandardWidth = model.StandardWidth;
        metadata.StandardHeight = model.StandardHeight;
        lock (MetadataLock)
        {
            cache.Upsert(metadata);
        }
    }

    public void ApplyNewMetadataDirectly(T2IModel model)
    {
        lock (ModificationLock)
        {
            if (model.Metadata is null || !model.RawFilePath.EndsWith(".safetensors"))
            {
                return;
            }
            using FileStream reader = File.OpenRead(model.RawFilePath);
            byte[] headerLen = new byte[8];
            reader.ReadExactly(headerLen, 0, 8);
            long len = BitConverter.ToInt64(headerLen, 0);
            if (len < 0 || len > 100 * 1024 * 1024)
            {
                Logs.Warning($"Model {model.Name} has invalid metadata length {len}.");
                return;
            }
            byte[] header = new byte[len];
            reader.ReadExactly(header, 0, (int)len);
            string headerStr = Encoding.UTF8.GetString(header);
            JObject json = JObject.Parse(headerStr);
            long pos = reader.Position;
            JObject metaHeader = (json["__metadata__"] as JObject) ?? new();
            if (!(metaHeader?.ContainsKey("modelspec.hash_sha256") ?? false))
            {
                metaHeader["modelspec.hash_sha256"] = "0x" + Utilities.BytesToHex(SHA256.HashData(reader));
            }
            void specSet(string key, string val)
            {
                if (!string.IsNullOrWhiteSpace(val))
                {
                    metaHeader[$"modelspec.{key}"] = val;
                }
                else
                {
                    metaHeader.Remove($"modelspec.{key}");
                }
            }
            specSet("sai_model_spec", "1.0.0");
            specSet("title", model.Metadata.Title);
            specSet("architecture", model.Metadata.ModelClassType);
            specSet("author", model.Metadata.Author);
            specSet("description", model.Metadata.Description);
            specSet("thumbnail", model.Metadata.PreviewImage);
            specSet("license", model.Metadata.License);
            specSet("usage_hint", model.Metadata.UsageHint);
            specSet("trigger_phrase", model.Metadata.TriggerPhrase);
            specSet("tags", string.Join(",", model.Metadata.Tags ?? Array.Empty<string>()));
            specSet("merged_from", model.Metadata.MergedFrom);
            specSet("date", model.Metadata.Date);
            specSet("preprocessor", model.Metadata.Preprocesor);
            specSet("resolution", $"{model.Metadata.StandardWidth}x{model.Metadata.StandardHeight}");
            if (model.Metadata.IsNegativeEmbedding)
            {
                specSet("is_negative_embedding", "true");
            }
            json["__metadata__"] = metaHeader;
            {
                using FileStream writer = File.OpenWrite(model.RawFilePath + ".tmp");
                byte[] headerBytes = Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
                writer.Write(BitConverter.GetBytes(headerBytes.LongLength));
                writer.Write(headerBytes);
                reader.Seek(pos, SeekOrigin.Begin);
                reader.CopyTo(writer);
                reader.Dispose();
            }
            // Journalling replace to prevent data loss in event of a crash.
            File.Move(model.RawFilePath, model.RawFilePath + ".tmp2");
            File.Move(model.RawFilePath + ".tmp", model.RawFilePath);
            File.Delete(model.RawFilePath + ".tmp2");
        }
    }

    private static readonly string[] AutoImageFormatSuffixes = new[] { ".jpg", ".png", ".preview.png", ".preview.jpg", ".thumb.jpg", ".thumb.png" };

    public string GetAutoFormatImage(T2IModel model)
    {
        string prefix = $"{FolderPath}/{model.Name.BeforeLast('.')}";
        foreach (string suffix in AutoImageFormatSuffixes)
        {
            if (File.Exists(prefix + suffix))
            {
                return new Image(File.ReadAllBytes(prefix + suffix), Image.ImageType.IMAGE, suffix.AfterLast('.')).ToMetadataFormat();
            }
        }
        return null;
    }

    /// <summary>Force-load the metadata for a model.</summary>
    public void LoadMetadata(T2IModel model)
    {
        if (model is null)
        {
            Logs.Warning($"Tried to load metadata for a null model?:\n{Environment.StackTrace}");
            return;
        }
        if (model.ModelClass is not null || (model.Title is not null && model.Title != model.Name.AfterLast('/')))
        {
            Logs.Debug($"Not loading metadata for {model.Name} as it is already loaded.");
            return;
        }
        if (!model.Name.EndsWith(".safetensors"))
        {
            string autoImg = GetAutoFormatImage(model);
            if (autoImg is not null)
            {
                model.PreviewImage = autoImg;
            }
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
            if (img is not null && !img.StartsWith("data:image/"))
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
            img ??= GetAutoFormatImage(model);
            metadata = new()
            {
                ModelFileVersion = modified,
                ModelName = fileName,
                ModelClassType = clazz?.ID,
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
                Preprocesor = metaHeader?.Value<string>("modelspec.preprocessor"),
                Tags = metaHeader?.Value<string>("modelspec.tags")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                IsNegativeEmbedding = metaHeader?.Value<string>("modelspec.is_negative_embedding") == "true"
            };
            lock (MetadataLock)
            {
                try
                {
                    cache.Upsert(metadata);
                }
                catch (Exception ex)
                {
                    Logs.Warning($"Error handling metadata database for model {model.RawFilePath}: {ex}");
                }
            }
        }
        lock (ModificationLock)
        {
            model.Title = metadata.Title;
            model.Description = metadata.Description;
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
        Logs.Verbose($"[Model Scan] Add all from folder {folder}");
        if (folder.StartsWith('.'))
        {
            return;
        }
        string prefix = folder == "" ? "" : $"{folder}/";
        string actualFolder = $"{FolderPath}/{folder}";
        if (!Directory.Exists(actualFolder))
        {
            Logs.Verbose($"[Model Scan] Skipping folder {actualFolder}");
            return;
        }
        foreach (string subfolder in Directory.EnumerateDirectories(actualFolder))
        {
            string path = $"{prefix}{subfolder.Replace('\\', '/').AfterLast('/')}";
            try
            {
                AddAllFromFolder(path);
            }
            catch (Exception ex)
            {
                Logs.Warning($"Error while scanning model subfolder '{path}': {ex}");
            }
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
                    Title = fullFilename.AfterLast('/'),
                    RawFilePath = file,
                    Description = "(Metadata not yet loaded.)",
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
            else if (fn.EndsWith(".ckpt") || fn.EndsWith(".pt") || fn.EndsWith(".pth"))
            {
                T2IModel model = new()
                {
                    Name = fullFilename,
                    RawFilePath = file,
                    Description = "(None, use '.safetensors' to enable metadata descriptions)",
                    PreviewImage = "imgs/legacy_ckpt.jpg",
                };
                model.PreviewImage = GetAutoFormatImage(model) ?? model.PreviewImage;
                Models[fullFilename] = model;
            }
        }
    }
}
