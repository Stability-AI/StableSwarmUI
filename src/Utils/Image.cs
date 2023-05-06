namespace StableUI.Utils;

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
}
