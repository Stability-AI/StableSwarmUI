using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using LiteDB;
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

    public record class ImageDatabase(LockObject Lock, LiteDatabase Database, ILiteCollection<ImageMetadataEntry> Metadata);

    /// <summary>Set of all image metadatabases, as a map from folder name to database.</summary>
    public static ConcurrentDictionary<string, ImageDatabase> Databases = new();

    /// <summary>Returns the database corresponding to the given folder path.</summary>
    public static ImageDatabase GetDatabaseForFolder(string folder)
    {
        return Databases.GetOrAdd(folder, f =>
        {
            LiteDatabase ldb = new(f + "/image_metadata.ldb");
            return new(new(), ldb, ldb.GetCollection<ImageMetadataEntry>("image_metadata"));
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
            string fileData = new Image(data).GetMetadata();
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
}
