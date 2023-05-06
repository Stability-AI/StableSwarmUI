using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableUI.Core;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace StableUI.Utils;

/// <summary>General utilities holder.</summary>
public static class Utilities
{
    /// <summary>StableUI's current version.</summary>
    public static readonly string Version = Assembly.GetEntryAssembly()?.GetName().Version.ToString();

    /// <summary>Used by linked pages to prevent cache errors when data changes.</summary>
    public static string VaryID = Version;

    /// <summary>Gets a secure hex string of a given length (will generate half as many bytes).</summary>
    public static string SecureRandomHex(int length)
    {
        if (length % 2 == 1)
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes((length + 1) / 2))[0..^1];
        }
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(length / 2));
    }

    /// <summary>Gets a convenient cancel token that cancels itself after a given time OR the program itself is cancelled.</summary>
    public static CancellationToken TimedCancel(TimeSpan time)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(Program.GlobalProgramCancel, new CancellationTokenSource(time).Token).Token;
    }

    /// <summary>Send JSON data to a WebSocket.</summary>
    public static async Task SendJson(this WebSocket socket, JObject obj, TimeSpan maxDuration)
    {
        await socket.SendAsync(StringConversionHelper.UTF8Encoding.GetBytes(obj.ToString(Formatting.None)), WebSocketMessageType.Text, true, TimedCancel(maxDuration));
    }

    /// <summary>Receive raw binary data from a WebSocket.</summary>
    public static async Task<byte[]> ReceiveData(this WebSocket socket, TimeSpan maxDuration, int maxBytes)
    {
        CancellationToken limit = TimedCancel(maxDuration);
        byte[] buffer = new byte[8192];
        using MemoryStream ms = new();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, limit);
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);
        return ms.ToArray();
    }

    /// <summary>Receive JSON data from a WebSocket.</summary>
    public static async Task<JObject> ReceiveJson(this WebSocket socket, TimeSpan maxDuration, int maxBytes)
    {
        string raw = Encoding.UTF8.GetString(await ReceiveData(socket, maxDuration, maxBytes));
        Console.WriteLine($"Received: {raw}");
        return JObject.Parse(raw);
    }
}
