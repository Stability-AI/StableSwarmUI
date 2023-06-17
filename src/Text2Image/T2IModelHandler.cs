using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableUI.Accounts;
using StableUI.Core;
using StableUI.Utils;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace StableUI.Text2Image;

/// <summary>Central manager for Text2Image models.</summary>
public class T2IModelHandler
{
    /// <summary>All models known to this handler.</summary>
    public ConcurrentDictionary<string, T2IModel> Models = new();

    /// <summary>Lock used when modifying the model list.</summary>
    public LockObject ModificationLock = new();

    public List<T2IModel> ListModelsFor(Session session)
    {
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
        lock (ModificationLock)
        {
            Models.Clear();
            AddAllFromFolder("");
        }
    }

    private void AddAllFromFolder(string folder)
    {
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
