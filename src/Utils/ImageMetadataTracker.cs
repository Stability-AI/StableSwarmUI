using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using LiteDB;
using StableSwarmUI.Core;
using System.IO;

namespace StableSwarmUI.Utils;

/// <summary>Helper class to track image file metadata.</summary>
public static class ImageMetadataTracker
{
    /// <summary>BSON database entry for image metadata.</summary>
    public class ImageMetadataEntry
    {
        [BsonId]
        public string FileName { get; set; }

        public string Metadata { get; set; }

        public long FileTime { get; set; }

        public long LastVerified { get; set; } // Reading file time can be slow, so don't do more than once per day per file.
    }

    /// <summary>BSON database entry for image preview thumbnails.</summary>
    public class ImagePreviewEntry
    {
        [BsonId]
        public string FileName { get; set; }

        public long FileTime { get; set; }

        public long LastVerified { get; set; }

        public byte[] PreviewData { get; set; }
    }

    public record class ImageDatabase(LockObject Lock, LiteDatabase Database, ILiteCollection<ImageMetadataEntry> Metadata, ILiteCollection<ImagePreviewEntry> Previews);

    /// <summary>Set of all image metadatabases, as a map from folder name to database.</summary>
    public static ConcurrentDictionary<string, ImageDatabase> Databases = new();

    /// <summary>Returns the database corresponding to the given folder path.</summary>
    public static ImageDatabase GetDatabaseForFolder(string folder)
    {
        return Databases.GetOrCreate(folder, () =>
        {
            LiteDatabase ldb = new(folder + "/image_metadata.ldb");
            return new(new(), ldb, ldb.GetCollection<ImageMetadataEntry>("image_metadata"), ldb.GetCollection<ImagePreviewEntry>("image_previews"));
        });
    }

    /// <summary>File format extensions that even can have metadata on them.</summary>
    public static HashSet<string> ExtensionsWithMetadata = new() { "png", "jpg", "webp" };

    /// <summary>Deletes any tracked metadata for the given filepath.</summary>
    public static void RemoveMetadataFor(string file)
    {
        string ext = file.AfterLast('.');
        if (!ExtensionsWithMetadata.Contains(ext))
        {
            return;
        }
        string folder = file.BeforeAndAfterLast('/', out string filename);
        ImageDatabase metadata = GetDatabaseForFolder(folder);
        lock (metadata.Lock)
        {
            metadata.Metadata.Delete(filename);
            metadata.Previews.Delete(filename);
        }
    }

    /// <summary>Get the preview bytes for the given image, going through a cache manager.</summary>
    public static byte[] GetOrCreatePreviewFor(string file)
    {
        string ext = file.AfterLast('.');
        if (!ExtensionsWithMetadata.Contains(ext))
        {
            return null;
        }
        string folder = file.BeforeAndAfterLast('/', out string filename);
        ImageDatabase metadata = GetDatabaseForFolder(folder);
        long timeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lock (metadata.Lock)
        {
            ImagePreviewEntry entry = metadata.Previews.FindById(filename);
            if (entry is not null)
            {
                if (Math.Abs(timeNow - entry.LastVerified) > 60 * 60 * 24)
                {
                    long fTime = ((DateTimeOffset)File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
                    if (entry.FileTime != fTime)
                    {
                        entry = null;
                    }
                    else
                    {
                        entry.LastVerified = timeNow;
                        metadata.Previews.Upsert(entry);
                    }
                }
                if (entry is not null)
                {
                    return entry.PreviewData;
                }
            }
        }
        if (!File.Exists(file))
        {
            return null;
        }
        long fileTime = ((DateTimeOffset)File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
        try
        {
            byte[] data = File.ReadAllBytes(file);
            if (data.Length == 0)
            {
                return null;
            }
            byte[] fileData = new Image(data, Image.ImageType.IMAGE, ext).ToMetadataJpg().ImageData;
            lock (metadata.Lock)
            {
                ImagePreviewEntry entry = new() { FileName = filename, PreviewData = fileData, LastVerified = timeNow, FileTime = fileTime };
                metadata.Previews.Upsert(entry);
            }
            return fileData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading image preview for file '{file}': {ex}");
            return null;
        }
    }

    /// <summary>Get the metadata text for the given file, going through a cache manager.</summary>
    public static string GetMetadataFor(string file)
    {
        string ext = file.AfterLast('.');
        if (!ExtensionsWithMetadata.Contains(ext))
        {
            return null;
        }
        string folder = file.BeforeAndAfterLast('/', out string filename);
        ImageDatabase metadata = GetDatabaseForFolder(folder);
        long timeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lock (metadata.Lock)
        {
            ImageMetadataEntry entry = metadata.Metadata.FindById(filename);
            if (entry is not null)
            {
                if (Math.Abs(timeNow - entry.LastVerified) > 60 * 60 * 24)
                {
                    long fTime = ((DateTimeOffset)File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
                    if (entry.FileTime != fTime)
                    {
                        entry = null;
                    }
                    else
                    {
                        entry.LastVerified = timeNow;
                        metadata.Metadata.Upsert(entry);
                    }
                }
                if (entry is not null)
                {
                    return entry.Metadata;
                }
            }
        }
        if (!File.Exists(file))
        {
            return null;
        }
        long fileTime = ((DateTimeOffset)File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
        try
        {
            byte[] data = File.ReadAllBytes(file);
            if (data.Length == 0)
            {
                return null;
            }
            string fileData = new Image(data, Image.ImageType.IMAGE, ext).GetMetadata();
            lock (metadata.Lock)
            {
                ImageMetadataEntry entry = new() { FileName = filename, Metadata = fileData, LastVerified = timeNow, FileTime = fileTime };
                metadata.Metadata.Upsert(entry);
            }
            return fileData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading image metadata for file '{file}': {ex}");
            return null;
        }
    }

    /// <summary>Shuts down and stores metadata helper files.</summary>
    public static void Shutdown()
    {
        ImageDatabase[] dbs = Databases.Values.ToArray();
        Databases.Clear();
        foreach (ImageDatabase db in dbs)
        {
            lock (db.Lock)
            {
                db.Database.Dispose();
            }
        }
    }

    public static void MassRemoveMetadata()
    {
        KeyValuePair<string, ImageDatabase>[] dbs = Databases.ToArray();
        foreach ((string name, ImageDatabase db) in dbs)
        {
            lock (db.Lock)
            {
                db.Database.Dispose();
                try
                {
                    File.Delete($"{name}/image_metadata.ldb");
                }
                catch (IOException) { }
                Databases.TryRemove(name, out _);
            }
        }
        static void ClearFolder(string folder)
        {
            if (File.Exists($"{folder}/image_metadata.ldb"))
            {
                try
                {
                    File.Delete($"{folder}/image_metadata.ldb");
                }
                catch (IOException) { }
            }
            foreach (string subFolder in Directory.GetDirectories(folder))
            {
                ClearFolder(subFolder);
            }
        }
        ClearFolder(Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, Program.ServerSettings.Paths.OutputPath));
    }
}
