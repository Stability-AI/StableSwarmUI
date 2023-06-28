using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using LiteDB;
using Newtonsoft.Json.Linq;
using StableUI.Accounts;
using StableUI.Core;
using System.IO;
using System.Text.RegularExpressions;

namespace StableUI.Text2Image;

/// <summary>Central manager for Text2Image models.</summary>
public class T2IModelHandler
{
    /// <summary>All models known to this handler.</summary>
    public ConcurrentDictionary<string, T2IModel> Models = new();

    /// <summary>Helper to sort model classes.</summary>
    public T2IModelClassSorter ClassSorter = new();

    /// <summary>Lock used when modifying the model list.</summary>
    public LockObject ModificationLock = new();

    public LiteDatabase ModelMetadataCache;

    public ILiteCollection<ModelMetadataStore> ModelMetadataCollection;

    public bool IsShutdown = false;

    /// <summary>Helper, data store for model metadata.</summary>
    public class ModelMetadataStore
    {
        [BsonId]
        public string ModelName { get; set; }

        public string ModelFileVersion { get; set; }

        public string ModelClassType { get; set; }

        public string ModelMetadataRaw { get; set; }
    }

    public T2IModelHandler()
    {
        Program.ModelRefreshEvent += Refresh;
        ModelMetadataCache = new(Program.ServerSettings.DataPath + "/model_metadata.ldb");
        ModelMetadataCollection = ModelMetadataCache.GetCollection<ModelMetadataStore>("model_metadata");
    }

    public void Shutdown()
    {
        if (IsShutdown)
        {
            return;
        }
        IsShutdown = true;
        lock (ModificationLock)
        {
            ModelMetadataCache?.Dispose();
            ModelMetadataCache = null;
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

    private void AddAllFromFolder(string folder)
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
        foreach (string subfolder in Directory.EnumerateDirectories($"{Program.ServerSettings.SDModelFullPath}/{folder}"))
        {
            AddAllFromFolder($"{prefix}{subfolder.AfterLast('/')}");
        }
        foreach (string file in Directory.EnumerateFiles($"{Program.ServerSettings.SDModelFullPath}/{folder}"))
        {
            string fn = file.Replace('\\', '/').AfterLast('/');
            string fullFilename = $"{prefix}{fn}";
            if (fn.EndsWith(".safetensors"))
            {
                Models[fullFilename] = new T2IModel()
                {
                    Name = fullFilename,
                    RawFilePath = file,
                    // TODO: scan these values from metadata
                    Description = "(TODO: Description)",
                    Type = "Unknown",
                    PreviewImage = "imgs/model_placeholder.jpg",
                };
            }
            else if (fn.EndsWith(".ckpt"))
            {
                Models[fullFilename] = new T2IModel()
                {
                    Name = fullFilename,
                    RawFilePath = file,
                    Description = "(None, use '.safetensors' to enable metadata descriptions)",
                    Type = "Unknown",
                    PreviewImage = "imgs/legacy_ckpt.jpg",
                };
            }
        }
    }
}
