using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Core;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace StableSwarmUI.Utils;

/// <summary>General utilities holder.</summary>
public static class Utilities
{
    /// <summary>StableSwarmUI's current version.</summary>
    public static readonly string Version = Assembly.GetEntryAssembly()?.GetName().Version.ToString();

    /// <summary>Used by linked pages to prevent cache errors when data changes.</summary>
    public static string VaryID = Version;

    /// <summary>Matcher for characters banned or specialcased by Windows or other OS's.</summary>
    public static AsciiMatcher FilePathForbidden = new(c => c < 32 || "<>:\"\\|?*~&@;".Contains(c));

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
        await socket.SendAsync(obj.ToString(Formatting.None).EncodeUTF8(), WebSocketMessageType.Text, true, TimedCancel(maxDuration));
    }

    /// <summary>Equivalent to <see cref="Task.WhenAny(IEnumerable{Task})"/> but doesn't break on an empty list.</summary>
    public static Task WhenAny(IEnumerable<Task> tasks)
    {
        if (tasks.IsEmpty())
        {
            return Task.CompletedTask;
        }
        return Task.WhenAny(tasks);
    }

    /// <summary>Equivalent to <see cref="Task.WhenAny(Task[])"/> but doesn't break on an empty list.</summary>
    public static Task WhenAny(params Task[] tasks)
    {
        if (tasks.IsEmpty())
        {
            return Task.CompletedTask;
        }
        return Task.WhenAny(tasks);
    }

    /// <summary>Receive raw binary data from a WebSocket.</summary>
    public static async Task<byte[]> ReceiveData(this WebSocket socket, int maxBytes, CancellationToken limit)
    {
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

    /// <summary>Receive raw binary data from a WebSocket.</summary>
    public static async Task<byte[]> ReceiveData(this WebSocket socket, TimeSpan maxDuration, int maxBytes)
    {
        return await ReceiveData(socket, maxBytes, TimedCancel(maxDuration));
    }

    /// <summary>Receive JSON data from a WebSocket.</summary>
    public static async Task<JObject> ReceiveJson(this WebSocket socket, int maxBytes, bool nullOnEmpty = false)
    {
        string raw = Encoding.UTF8.GetString(await ReceiveData(socket, maxBytes, Program.GlobalProgramCancel));
        if (nullOnEmpty && string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        return raw.ParseToJson();
    }

    /// <summary>Receive JSON data from a WebSocket.</summary>
    public static async Task<JObject> ReceiveJson(this WebSocket socket, TimeSpan maxDuration, int maxBytes, bool nullOnEmpty = false)
    {
        string raw = Encoding.UTF8.GetString(await ReceiveData(socket, maxDuration, maxBytes));
        if (nullOnEmpty && string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        return raw.ParseToJson();
    }

    /// <summary>Converts the JSON data to predictable basic data.</summary>
    public static object ToBasicObject(this JToken token)
    {
        return token.Type switch
        {
            JTokenType.Object => ((JObject)token).ToBasicObject(),
            JTokenType.Array => ((JArray)token).Select(ToBasicObject).ToList(),
            JTokenType.Integer => (long)token,
            JTokenType.Float => (double)token,
            JTokenType.String => (string)token,
            JTokenType.Boolean => (bool)token,
            JTokenType.Null => null,
            _ => throw new Exception("Unknown token type: " + token.Type),
        };
    }

    /// <summary>Converts the JSON data to predictable basic data.</summary>
    public static Dictionary<string, object> ToBasicObject(this JObject obj)
    {
        Dictionary<string, object> result = new();
        foreach ((string key, JToken val) in obj)
        {
            result[key] = val.ToBasicObject();
        }
        return result;
    }

    public static async Task YieldJsonOutput(this HttpContext context, WebSocket socket, int status, JObject obj)
    {
        if (socket != null)
        {
            await socket.SendJson(obj, TimeSpan.FromMinutes(1));
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, TimedCancel(TimeSpan.FromMinutes(1)));
            return;
        }
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = status;
        await context.Response.WriteAsync(obj.ToString(Formatting.None));
        await context.Response.CompleteAsync();
    }

    public static JObject ErrorObj(string message, string error_id)
    {
        return new JObject() { ["error"] = message, ["error_id"] = error_id };
    }

    public static ByteArrayContent JSONContent(JObject jobj)
    {
        ByteArrayContent content = new(jobj.ToString(Formatting.None).EncodeUTF8());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    public static string EscapeJsonString(string input)
    {
        string cleaned = input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\b", "\\b").Replace("\t", "\\t").Replace("\f", "\\f").Replace("/", "\\/");
        StringBuilder output = new(input.Length);
        foreach (char c in cleaned)
        {
            if (c < 32)
            {
                output.Append("\\u");
                output.Append(((int)c).ToString("X4"));
            }
            else
            {
                output.Append(c);
            }
        }
        return output.ToString();
    }

    /// <summary>A mapping of common file extensions to their content type.</summary>
    public static Dictionary<string, string> CommonContentTypes = new()
        {
            { "png", "image/png" },
            { "jpg", "image/jpeg" },
            { "jpeg", "image/jpeg" },
            { "gif", "image/gif" },
            { "ico", "image/x-icon" },
            { "svg", "image/svg+xml" },
            { "mp3", "audio/mpeg" },
            { "wav", "audio/x-wav" },
            { "js", "application/javascript" },
            { "ogg", "application/ogg" },
            { "json", "application/json" },
            { "zip", "application/zip" },
            { "dat", "application/octet-stream" },
            { "css", "text/css" },
            { "htm", "text/html" },
            { "html", "text/html" },
            { "txt", "text/plain" },
            { "yml", "text/plain" },
            { "fds", "text/plain" },
            { "xml", "text/xml" },
            { "mp4", "video/mp4" },
            { "mpeg", "video/mpeg" },
            { "webm", "video/webm" }
        };

    /// <summary>Guesses the content type based on path for common file types.</summary>
    public static string GuessContentType(string path)
    {
        string extension = path.AfterLast('.');
        return CommonContentTypes.GetValueOrDefault(extension, "application/octet-stream");
    }

    public static JObject ParseToJson(this string input)
    {
        try
        {
            return JObject.Parse(input);
        }
        catch (JsonReaderException ex)
        {
            throw new JsonReaderException($"Failed to parse JSON `{input.Replace("\n", "  ")}`: {ex.Message}");
        }
    }

    public static Dictionary<string, T> ApplyMap<T>(Dictionary<string, T> orig, Dictionary<string, string> map)
    {
        Dictionary<string, T> result = new(orig);
        foreach ((string mapFrom, string mapTo) in map)
        {
            if (result.Remove(mapFrom, out T value))
            {
                result[mapTo] = value;
            }
        }
        return result;
    }

    public static Task RunCheckedTask(Action action)
    {
        return Task.Run(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logs.Error($"Internal error in async task: {ex}");
            }
        });
    }

    public static Task RunCheckedTask(Func<Task> action)
    {
        return Task.Run(() =>
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                Logs.Error($"Internal error in async task: {ex}");
                return Task.CompletedTask;
            }
        });
    }

    /// <summary>Returns whether a given port number is taken (there is already a program listening on that port).</summary>
    public static bool IsPortTaken(int port)
    {
        return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == port);
    }

    /// <summary>Kill system process..</summary>
    [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
    public static extern int sys_kill(int pid, int signal);
}
