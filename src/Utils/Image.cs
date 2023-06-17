namespace StableUI.Utils;

using FreneticUtilities.FreneticToolkit;
using SixLabors.ImageSharp;
using System.IO;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using ISImage = SixLabors.ImageSharp.Image;

/// <summary>Helper to represent an image file cleanly and quickly.</summary>
public class Image
{
    /// <summary>The raw binary data.</summary>
    public byte[] ImageData;

    /// <summary>Construct an image from Base64 text.</summary>
    public Image(string base64) : this(Convert.FromBase64String(base64))
    {
    }

    /// <summary>Construct an image from raw binary data.</summary>
    public Image(byte[] data)
    {
        ImageData = data;
    }

    /// <summary>Get a Base64 string representation of the raw image data.</summary>
    public string AsBase64 => Convert.ToBase64String(ImageData);

    /// <summary>Gets an ImageSharp <see cref="ISImage"/> for this image.</summary>
    public ISImage ToIS => ISImage.Load(ImageData);

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

    /// <summary>Converts an image to the specified format, and the specific metadata text.</summary>
    public Image ConvertTo(string format, string metadata = null)
    {
        using MemoryStream ms = new();
        ISImage img = ToIS;
        img.Metadata.XmpProfile = null;
        if (metadata is null)
        {
            img.Metadata.ExifProfile = null;
        }
        else
        {
            ExifProfile prof = new();
            prof.SetValue(ExifTag.Copyright, metadata); // TODO: More appropriate metadata method?
            img.Metadata.ExifProfile = prof;
        }
        switch (format)
        {
            case "png":
                img.SaveAsPng(ms);
                break;
            case "jpg":
                img.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder() { Quality = 100 });
                break;
            case "jpg90":
                img.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder() { Quality = 90 });
                break;
            case "jpg75":
                img.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder() { Quality = 75 });
                break;
                // TODO: webp, etc. with appropriate quality handlers
        }
        return new(ms.ToArray());
    }
}
