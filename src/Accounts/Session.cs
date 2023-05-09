using Microsoft.AspNetCore.StaticFiles;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Utils;

namespace StableUI.Accounts;

/// <summary>Container for information related to an active session.</summary>
public class Session
{
    /// <summary>Randomly generated ID.</summary>
    public string ID;

    /// <summary>The relevant <see cref="User"/>.</summary>
    public User User;

    /// <summary>Save an image as this user, and returns the new URL. If user has disabled saving, returns a data URL.</summary>
    public string SaveImage(Image image, T2IParams user_input)
    {
        // TODO: File conversion, metadata, etc.
        if (!User.Settings.SaveFiles)
        {
            return "data:image/png;base64," + image.AsBase64;
        }
        string imagePath = User.BuildImageOutputPath(user_input);
        string extension = "png";
        string fullPath = $"{User.OutputDirectory}/{imagePath}.{extension}";
        lock (User.UserLock)
        {
            try
            {
                int num = 0;
                while (File.Exists($"{imagePath}-{(num == 0 ? "" : num)}.{extension}"))
                {
                    num++;
                }
                Directory.CreateDirectory(Directory.GetParent(fullPath).FullName);
                File.WriteAllBytes(fullPath, image.ImageData);
            }
            catch (Exception)
            {
                try
                {
                    imagePath = "image_name_error/" + Utilities.SecureRandomHex(10);
                    fullPath = $"{User.OutputDirectory}/{imagePath}.{extension}";
                    Directory.CreateDirectory(Directory.GetParent(fullPath).FullName);
                    File.WriteAllBytes(fullPath, image.ImageData);
                }
                catch (Exception ex)
                {
                    Logs.Error($"Could not save user image: {ex.Message}");
                    return "ERROR";
                }
            }
        }
        return $"Output/{imagePath}.{extension}";
    }
}
