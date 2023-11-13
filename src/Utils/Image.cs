namespace StableSwarmUI.Utils;

using SixLabors.ImageSharp;
using System.IO;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using ISImage = SixLabors.ImageSharp.Image;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp.Processing;
using FreneticUtilities.FreneticExtensions;

/// <summary>Helper to represent an image file cleanly and quickly.</summary>
public class Image
{
    /// <summary>The raw binary data.</summary>
    public byte[] ImageData;

    public enum ImageType
    {
        IMAGE = 0,
        /// <summary>ie animated gif</summary>
        ANIMATION = 1,
        VIDEO = 2
    }

    /// <summary>The type of image data this image holds.</summary>
    public ImageType Type;

    /// <summary>File extension for this image.</summary>
    public string Extension;

    /// <summary>Creates an image object from a web image data URL string.</summary>
    public static Image FromDataString(string data)
    {
        if (data.StartsWith("data:video/"))
        {
            string ext = data.After("data:video/").Before(";base64,");
            return new Image(data.ToString().After(";base64,"), ImageType.VIDEO, ext);
        }
        if (data.StartsWith("data:image/gif;"))
        {
            return new Image(data.ToString().After(";base64,"), ImageType.ANIMATION, "gif");
        }
        if (data.StartsWith("data:image/"))
        {
            string ext = data.After("data:image/").Before(";base64,");
            if (ext == "jpeg")
            {
                ext = "jpg";
            }
            return new Image(data.ToString().After(";base64,"), ImageType.IMAGE, ext);
        }
        return new Image(data.ToString().After(";base64,"), ImageType.IMAGE, "png");
    }

    /// <summary>Construct an image from Base64 text.</summary>
    public Image(string base64, ImageType type, string extension) : this(Convert.FromBase64String(base64), type, extension)
    {
    }

    /// <summary>Construct an image from raw binary data.</summary>
    public Image(byte[] data, ImageType type, string extension)
    {
        Extension = extension;
        Type = type;
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        else if (data.Length == 0)
        {
              throw new ArgumentException("Data is empty!", nameof(data));
        }
        ImageData = data;
    }

    /// <summary>Construct an image from an ISImage.</summary>
    public Image(ISImage img) : this(ISImgToPngBytes(img), ImageType.IMAGE, "png")
    {
    }

    /// <summary>Get a Base64 string representation of the raw image data.</summary>
    public string AsBase64 => Convert.ToBase64String(ImageData);

    /// <summary>Gets an ImageSharp <see cref="ISImage"/> for this image.</summary>
    public ISImage ToIS => ISImage.Load(ImageData);

    /// <summary>Helper to convert an ImageSharp image to png bytes.</summary>
    public static byte[] ISImgToPngBytes(ISImage img)
    {
        using MemoryStream stream = new();
        img.SaveAsPng(stream);
        return stream.ToArray();
    }

    /// <summary>Helper to convert an ImageSharp image to jpg bytes.</summary>
    public static byte[] ISImgToJpgBytes(ISImage img)
    {
        using MemoryStream stream = new();
        img.SaveAsJpeg(stream);
        return stream.ToArray();
    }

    public string AsDataString()
    {
        if (Type == ImageType.ANIMATION)
        {
            return "data:image/gif;base64," + AsBase64;
        }
        else if (Type == ImageType.VIDEO)
        {
            return $"data:video/{Extension};base64," + AsBase64;
        }
        return $"data:image/{(Extension == "jpg" ? "jpeg" : Extension)};base64," + AsBase64;
    }

    /// <summary>Returns a metadata-format of the image.</summary>
    public string ToMetadataFormat()
    {
        if (Type != ImageType.IMAGE)
        {
            return AsDataString();
        }
        ISImage img = ToIS;
        float factor = 256f / Math.Min(img.Width, img.Height);
        img.Mutate(i => i.Resize((int)(img.Width * factor), (int)(img.Height * factor)));
        return "data:image/jpeg;base64," + new Image(ISImgToJpgBytes(img), Type, "jpg").AsBase64;
    }

    /// <summary>Resizes the given image directly and returns a png formatted copy of it.</summary>
    public Image Resize(int width, int height)
    {
        if (Type != ImageType.IMAGE)
        {
            return this;
        }
        ISImage img = ToIS;
        if (ToIS.Width == width && ToIS.Height == height)
        {
            return this;
        }
        img.Mutate(i => i.Resize(width, height));
        return new(ISImgToPngBytes(img), Type, Extension);
    }

    /// <summary>Returns a copy of this image that's definitely in '.png' format.</summary>
    public Image ForceToPng()
    {
        if (Type != ImageType.IMAGE)
        {
            return this;
        }
        return new(ISImgToPngBytes(ToIS), Type, "png");
    }

    /// <summary>Image formats that are possible to save as.</summary>
    public enum ImageFormat
    {
        /// <summary>PNG: Lossless, big file.</summary>
        PNG,
        /// <summary>JPEG: Lossy, (100% quality), small file.</summary>
        JPG,
        /// <summary>JPEG: Lossy, (90% quality), small file.</summary>
        JPG90,
        /// <summary>JPEG: Lossy, (bad 75% quality), small file.</summary>
        JPG75
    }

    /// <summary>Returns the metadata from this image, or null if none.</summary>
    public string GetMetadata()
    {
        try
        {
            ISImage img = ToIS;
            string pngMetadata = img.Metadata?.GetPngMetadata()?.TextData?.FirstOrDefault(t => t.Keyword.ToLowerFast() == "parameters").Value;
            if (pngMetadata is not null)
            {
                return pngMetadata;
            }
            if (img.Metadata?.ExifProfile?.TryGetValue(ExifTag.Model, out var data) ?? false)
            {
                return data.Value;
            }
        }
        catch (ArgumentNullException ex)
        {
            Logs.Verbose("Failed image content: " + AsBase64);
            Logs.Error($"Metadata read for image failed: {ex.Message}");
        }
        return null;
    }

    /// <summary>Gets the metadata JSON from an image generated by this program.</summary>
    public JObject GetSUIMetadata()
    {
        return GetMetadata()?.ParseToJson()?["sui_image_params"]?.Value<JObject>();
    }

    /// <summary>Converts an image to the specified format, and the specific metadata text.</summary>
    public Image ConvertTo(string format, string metadata = null, int dpi = 0)
    {
        if (Type != ImageType.IMAGE)
        {
            return this;
        }
        using MemoryStream ms = new();
        ISImage img = ToIS;
        img.Metadata.XmpProfile = null;
        img.Metadata.ExifProfile = null;
        ExifProfile prof = new();
        bool useExif = false;
        if (dpi > 0)
        {
            prof.SetValue(ExifTag.XResolution, new Rational((uint)dpi, 1));
            prof.SetValue(ExifTag.YResolution, new Rational((uint)dpi, 1));
            prof.SetValue(ExifTag.ResolutionUnit, (ushort)2);
            img.Metadata.HorizontalResolution = dpi;
            img.Metadata.VerticalResolution = dpi;
            useExif = true;
        }
        if (format == "PNG")
        {
            img.Metadata.GetPngMetadata().TextData.Add(new("Parameters", metadata, null, null));
        }
        else
        {
            prof.SetValue(ExifTag.UserComment, metadata);
            useExif = true;
        }
        if (useExif)
        {
            img.Metadata.ExifProfile = prof;
        }
        string ext = "jpg";
        switch (format)
        {
            case "PNG":
                img.SaveAsPng(ms);
                ext = "png";
                break;
            case "JPG":
                img.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder() { Quality = 100 });
                break;
            case "JPG90":
                img.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder() { Quality = 90 });
                break;
            case "JPG75":
                img.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder() { Quality = 75 });
                break;
            // TODO: webp, etc. with appropriate quality handlers
            default:
                throw new InvalidDataException($"User setting for image format is '{format}', which is invalid");
        }
        return new(ms.ToArray(), Type, ext);
    }
}
