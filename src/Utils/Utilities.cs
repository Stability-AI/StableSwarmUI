using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System;
using System.Net;
using System.Diagnostics;
using StableSwarmUI.Text2Image;
using System.Net.Sockets;

namespace StableSwarmUI.Utils;

/// <summary>General utilities holder.</summary>
public static class Utilities
{
    /// <summary>Preps various utilities during server start.</summary>
    public static void PrepUtils()
    {
        ThreadPool.SetMinThreads(512, 512);
        Program.TickIsGeneratingEvent += () => WebhookManager.WaitUntilCanStartGenerating().Wait();
        Program.TickNoGenerationsEvent += () => WebhookManager.TickNoGenerations().Wait();
        Program.TickIsGeneratingEvent += MemCleaner.TickIsGenerating;
        Program.TickNoGenerationsEvent += MemCleaner.TickNoGenerations;
        Program.TickEvent += SystemStatusMonitor.Tick;
        new Thread(TickLoop).Start();
    }

    /// <summary>Internal tick loop thread main method.</summary>
    public static void TickLoop()
    {
        while (!Program.GlobalProgramCancel.IsCancellationRequested)
        {
            try
            {
                Task.Delay(TimeSpan.FromSeconds(1), Program.GlobalProgramCancel).Wait(Program.GlobalProgramCancel);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            try
            {
                Program.TickEvent?.Invoke();
            }
            catch (Exception ex)
            {
                Logs.Error($"Tick loop encountered exception: {ex}");
            }
        }
    }

    /// <summary>StableSwarmUI's current version.</summary>
    public static readonly string Version = Assembly.GetEntryAssembly()?.GetName().Version.ToString();

    /// <summary>URL to where the documentation files start.</summary>
    public static string RepoDocsRoot = "https://github.com/Stability-AI/StableSwarmUI/blob/master/docs/";

    /// <summary>Current git commit (if known -- empty if unknown).</summary>
    public static string GitCommit = "";

    /// <summary>Used by linked pages to prevent cache errors when data changes.</summary>
    public static string VaryID = Version;

    /// <summary>A temporary unique ID for this server, used to make sure we don't ever form a circular swarm connection path.</summary>
    public static Guid LoopPreventionID = Guid.NewGuid();

    /// <summary>Matcher for ASCII control codes (including newlines, etc).</summary>
    public static AsciiMatcher ControlCodesMatcher = new(c => c < 32);

    /// <summary>Matcher for characters banned or specialcased by Windows or other OS's.</summary>
    public static AsciiMatcher FilePathForbidden = new(c => c < 32 || "<>:\"\\|?*~&@;#$^".Contains(c));

    public static HashSet<string> ReservedFilenames = ["con", "prn", "aux", "nul"];

    static Utilities()
    {
        if (File.Exists("./.git/refs/heads/master"))
        {
            GitCommit = File.ReadAllText("./.git/refs/heads/master").Trim()[0..8];
            VaryID += ".GIT-" + GitCommit;
        }
        for (int i = 0; i <= 9; i++)
        {
            ReservedFilenames.Add($"com{i}");
            ReservedFilenames.Add($"lpt{i}");
        }
    }

    /// <summary>Cleans a filename with strict filtering, including removal of forbidden characters, removal of the '.' symbol, but permitting '/'.</summary>
    public static string StrictFilenameClean(string name)
    {
        name = FilePathForbidden.TrimToNonMatches(name.Replace('\\', '/')).Replace(".", "");
        while (name.Contains("//"))
        {
            name = name.Replace("//", "/");
        }
        name = name.Trim();
        string[] parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (ReservedFilenames.Contains(parts[i].ToLowerFast()))
            {
                parts[i] = $"{parts[i]}_";
            }
        }
        return parts.JoinString("/");
    }

    /// <summary>Mini-utility class to debug load times.</summary>
    public class LoadTimer
    {
        public long StartTime = Environment.TickCount64;
        public long LastTime = Environment.TickCount64;

        public void Check(string part)
        {
            long timeNow = Environment.TickCount64;
            Logs.Debug($"[Load Time] {part} took {(timeNow - LastTime) / 1000.0:0.##}s ({(timeNow - StartTime) / 1000.0:0.##}s from start)");
            LastTime = timeNow;
        }
    }

    /// <summary>Mini-utility class to debug timings.</summary>
    public class ChunkedTimer
    {
        public long StartTime = Environment.TickCount64;
        public long LastTime = Environment.TickCount64;

        public Dictionary<string, (long, long)> Times = [];

        public void Reset()
        {
            StartTime = Environment.TickCount64;
            LastTime = Environment.TickCount64;
        }

        public void Mark(string part)
        {
            long timeNow = Environment.TickCount64;
            Times[part] = (timeNow - LastTime, timeNow - StartTime);
            LastTime = timeNow;
        }

        public void Debug(string extra)
        {
            string content = Times.Select(kvp => $"{kvp.Key}: {kvp.Value.Item1 / 1000.0:0.##}s ({kvp.Value.Item2 / 1000.0:0.##}s from start)").JoinString(", ");
            Logs.Debug($"[ChunkedTimer] {content} {extra}");
        }
    }

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
            if (ms.Length > maxBytes)
            {
                throw new IOException($"Received too much data! (over {maxBytes} bytes)");
            }
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

    /// <summary>Sends a JSON object post and receives a JSON object back.</summary>
    public static async Task<JObject> PostJson(this HttpClient client, string url, JObject data)
    {
        return (await (await client.PostAsync(url, JSONContent(data), Program.GlobalProgramCancel)).Content.ReadAsStringAsync()).ParseToJson();
    }

    /// <summary>Sends a JSON string post and receives a JSON object back.</summary>
    public static async Task<JObject> PostJSONString(this HttpClient client, string route, string input, CancellationToken interrupt)
    {
        return await NetworkBackendUtils.Parse<JObject>(await client.PostAsync(route, new StringContent(input, StringConversionHelper.UTF8Encoding, "application/json"), interrupt));
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
        Dictionary<string, object> result = [];
        foreach ((string key, JToken val) in obj)
        {
            result[key] = val.ToBasicObject();
        }
        return result;
    }

    /// <summary>Sorts the data in a <see cref="JObject"/> by the given key processing function.</summary>
    public static JObject SortByKey<TSortable>(this JObject obj, Func<string, TSortable> sort)
    {
        return JObject.FromObject(obj.Properties().OrderBy(p => sort(p.Name)).ToDictionary(p => p.Name, p => p.Value));
    }

    /// <summary>Gives a clean standard 4-space serialize of this <see cref="JObject"/>.</summary>
    public static string SerializeClean(this JObject jobj)
    {
        // Why is JSON.NET's API so weirdly splintered? So many different fundamental routes needed to get access to basic settings.
        using StringWriter sw = new();
        using JsonTextWriter jw = new(sw);
        jw.Formatting = Formatting.Indented;
        jw.IndentChar = ' ';
        jw.Indentation = 4;
        JsonSerializer serializer = new();
        serializer.Serialize(jw, jobj);
        jw.Flush();
        return sw.ToString() + Environment.NewLine;

    }

    public static async Task YieldJsonOutput(this HttpContext context, WebSocket socket, int status, JObject obj)
    {
        if (socket != null)
        {
            await socket.SendJson(obj, TimeSpan.FromMinutes(1));
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, TimedCancel(TimeSpan.FromMinutes(1)));
            return;
        }
        byte[] resp = obj.ToString(Formatting.None).EncodeUTF8();
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = status;
        context.Response.ContentLength = resp.Length;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.BodyWriter.WriteAsync(resp, Program.GlobalProgramCancel);
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

    /// <summary>Takes an escaped JSON string, and returns the plaintext unescaped form of it.</summary>
    public static string UnescapeJsonString(string input)
    {
        return JObject.Parse("{ \"value\": \"" + input + "\" }")["value"].ToString();
    }

    /// <summary>Accelerator trick to speed up <see cref="EscapeJsonString(string)"/>.</summary>
    public static AsciiMatcher NeedsJsonEscapeMatcher = new(c => c < 32 || "\\\"\n\r\b\t\f/".Contains(c, StringComparison.Ordinal));

    /// <summary>Takes a string that may contain unpredictable content, and escapes it to fit safely within a JSON string section.</summary>
    public static string EscapeJsonString(string input)
    {
        if (!NeedsJsonEscapeMatcher.ContainsAnyMatch(input))
        {
            return input;
        }
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
            { "webp", "image/webp" },
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
            { "mov", "video/quicktime" },
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

    /// <summary>Runs a task async with an exception check.</summary>
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

    /// <summary>Kill system process.</summary>
    [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
    public static extern int sys_kill(int pid, int signal);

    /// <summary>Attempt to properly kill a process.</summary>
    public static void KillProcess(Process proc, int graceSeconds)
    {
        if (proc is null || proc.HasExited)
        {
            return;
        }
        try
        {
            sys_kill(proc.Id, 15); // try graceful exit (SIGTERM=15)
            proc.WaitForExit(TimeSpan.FromSeconds(graceSeconds));
        }
        catch (DllNotFoundException)
        {
            Logs.Verbose($"Utilities.KillProcess: DllNotFoundException for libc");
            // Sometimes libc just isn't available (Windows especially) so just ignore those failures, ungraceful kill only I guess.
        }
        proc.Kill(true); // Now kill the full tree (SIGKILL=9)
        proc.WaitForExit(TimeSpan.FromSeconds(graceSeconds));
        if (!proc.HasExited)
        {
            proc.Kill(); // Make really sure it's dead (SIGKILL=9)
        }
    }

    /// <summary>Reusable general web client.</summary>
    public static HttpClient UtilWebClient = NetworkBackendUtils.MakeHttpClient();

    /// <summary>Downloads a file from a given URL and saves it to a given filepath.</summary>
    public static async Task DownloadFile(string url, string filepath, Action<long, long> progressUpdate)
    {
        using FileStream writer = File.OpenWrite(filepath);
        using HttpResponseMessage response = await UtilWebClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseHeadersRead, Program.GlobalProgramCancel);
        long length = response.Content.Headers.ContentLength ?? 0;
        byte[] buffer = new byte[Math.Min(length + 1024, 1024 * 1024 * 64)]; // up to 64 megabytes, just grab as big a chunk as we can at a time
        long progress = 0;
        long lastUpdate = Environment.TickCount64;
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Failed to download {url}: got response code {(int)response.StatusCode} {response.StatusCode}");
        }
        using Stream dlStream = await response.Content.ReadAsStreamAsync();
        progressUpdate?.Invoke(0, length);
        while (true)
        {
            int read = await dlStream.ReadAsync(buffer, Program.GlobalProgramCancel);
            if (read <= 0)
            {
                Logs.Verbose($"Download {url} completed with {progress} bytes.");
                if (length != 0 && progress != length)
                {
                    throw new InvalidOperationException($"Download {url} failed: expected {length} bytes but got {progress} bytes.");
                }
                progressUpdate?.Invoke(progress, length);
                return;
            }
            progress += read;
            if (Environment.TickCount64 - lastUpdate > 1000)
            {
                Logs.Verbose($"Download {url} now at {new MemoryNum(progress)} / {new MemoryNum(length)}... {(progress / (double)length) * 100:00.0}%");
                progressUpdate?.Invoke(progress, length);
                lastUpdate = Environment.TickCount64;
            }
            await writer.WriteAsync(buffer.AsMemory(0, read), Program.GlobalProgramCancel);
        }
    }

    /// <summary>Converts a byte array to a hexadecimal string.</summary>
    public static string BytesToHex(byte[] raw)
    {
        static char getHexChar(int val) => (char)((val < 10) ? ('0' + val) : ('a' + (val - 10)));
        char[] res = new char[raw.Length * 2];
        for (int i = 0; i < raw.Length; i++)
        {
            res[i << 1] = getHexChar((raw[i] & 0xF0) >> 4);
            res[(i << 1) + 1] = getHexChar(raw[i] & 0x0F);
        }
        return new string(res);
    }

    /// <summary>Computes the SHA 256 hash of a byte array and returns it as plaintext.</summary>
    public static string HashSHA256(byte[] raw)
    {
        return BytesToHex(SHA256.HashData(raw));
    }

    /// <summary>Smart clean combination of two paths in a way that allows B or C to be an absolute path.</summary>
    public static string CombinePathWithAbsolute(string a, string b, string c) => CombinePathWithAbsolute(CombinePathWithAbsolute(a, b), c);

    /// <summary>Smart clean combination of two paths in a way that allows B to be an absolute path.</summary>
    public static string CombinePathWithAbsolute(string a, string b)
    {
        if (b.StartsWith('/') || (b.Length > 2 && b[1] == ':') || b.StartsWith("\\\\"))
        {
            return b;
        }
        // Usage of '/' is always standard, but if we're exclusively using '\' windows backslashes in input, preserve them for the purposes of this method.
        char separator = (a.Contains('/') || b.Contains('/')) ? '/' : Path.DirectorySeparatorChar;
        if (a.EndsWith(separator))
        {
            return $"{a}{b}";
        }
        string result = $"{a}{separator}{b}";
        while (result.Contains($"{separator}{separator}"))
        {
            result = result.Replace($"{separator}{separator}", $"{separator}");
        }
        return result;
    }

    /// <summary>Rounds a number to the given precision.</summary>
    public static double RoundToPrecision(double val, double prec)
    {
        return Math.Round(val / prec) * prec;
    }

    /// <summary>Modifies a width/height resolution to get the nearest valid resolution for the model's megapixel target scale, and rounds to a factor of x64.</summary>
    public static (int, int) ResToModelFit(int width, int height, T2IModel model)
    {
        int modelWid = model.StandardWidth <= 0 ? width : model.StandardWidth;
        int modelHei = model.StandardHeight <= 0 ? height : model.StandardHeight;
        return ResToModelFit(width, height, modelWid * modelHei);
    }

    /// <summary>Modifies a width/height resolution to get the nearest valid resolution for the given megapixel target scale, and rounds to a factor of x64.</summary>
    public static (int, int) ResToModelFit(int width, int height, int mpTarget)
    {
        int mp = width * height;
        double scale = Math.Sqrt(mpTarget / (double)mp);
        int newWid = (int)RoundToPrecision(width * scale, 64);
        int newHei = (int)RoundToPrecision(height * scale, 64);
        return (newWid, newHei);
    }

    /// <summary>Gets a dense but trimmed string representation of JSON data, for debugging.</summary>
    public static string ToDenseDebugString(this JToken jData, bool noSpacing = false, int partCharLimit = 256, string spaces = "")
    {
        if (jData is null)
        {
            return null;
        }
        if (jData is JObject jObj)
        {
            string subSpaces = spaces + "    ";
            string resultStr = jObj.Properties().Select(v => $"\"{v.Name}\": {v.Value.ToDenseDebugString(noSpacing, partCharLimit, subSpaces)}").JoinString(", ");
            if (resultStr.Length <= 50 || noSpacing)
            {
                return "{ " + resultStr + " }";
            }
            return "{\n" + subSpaces + resultStr + "\n" + spaces + "}";
        }
        else if (jData is JArray jArr)
        {
            string subSpaces = spaces + "    ";
            string resultStr = jArr.Select(v => v.ToDenseDebugString(noSpacing, partCharLimit, subSpaces)).JoinString(", ");
            if (resultStr.Length == 0)
            {
                return "[ ]";
            }
            if (resultStr.Length <= 50 || noSpacing)
            {
                return $"[ {resultStr} ]";
            }
            return $"[\n{subSpaces}{resultStr}\n{spaces}]";
        }
        else
        {
            if (jData.Type == JTokenType.Null)
            {
                return "null";
            }
            else if (jData.Type == JTokenType.Integer || jData.Type == JTokenType.Float || jData.Type == JTokenType.Boolean)
            {
                return jData.ToString();
            }
            string val = jData.ToString();
            if (val.Length > partCharLimit - 3)
            {
                val = val[..(partCharLimit - 3)] + "...";
            }
            val = val.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace("\"", "\\\"");
            return $"\"{val}\"";
        }
    }

    /// <summary>Quick helper to nuke old pycaches, because python leaves them lying around and does not clean up after itself :(
    /// Useful for removing old python folders that have been removed from git.</summary>
    public static void RemoveBadPycacheFrom(string path)
    {
        try
        {
            string potentialCache = $"{path}/__pycache__/";
            if (!Directory.Exists(potentialCache))
            {
                return;
            }
            string[] files = Directory.GetFileSystemEntries(potentialCache);
            if (files.Any(f => !f.EndsWith(".pyc"))) // Safety backup: if this cache has non-pycache files, we can't safely delete it.
            {
                return;
            }
            foreach (string file in files)
            {
                File.Delete(file);
            }
            Directory.Delete(potentialCache);
            if (Directory.EnumerateFileSystemEntries(path).IsEmpty())
            {
                Directory.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Logs.Debug($"Failed to remove bad pycache from {path}: {ex}");
        }
    }

    /// <summary>Tries to read the local IP address, if possible. Returns null if not found. Value may be wrong or misleading.</summary>
    public static string GetLocalIPAddress()
    {
        IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
        List<string> result = [];
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork && !$"{ip}".EndsWith(".1"))
            {
                result.Add($"http://{ip}:{Program.ServerSettings.Network.Port}");
            }
        }
        if (result.Any())
        {
            return result.JoinString(", ");
        }
        return null;
    }

    /// <summary>Cause an immediate aggressive RAM cleanup.</summary>
    public static void CleanRAM()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
    }

    public static string DotNetVersMissing = null;

    /// <summary>Check if a dotnet version is installed, and, if not, show a log message and write to a utility flag.</summary>
    public static void CheckDotNet(string vers)
    {
        Task.Run(() =>
        {
            try
            {
                Process p = Process.Start(new ProcessStartInfo("dotnet", "--list-sdks") { RedirectStandardOutput = true, UseShellExecute = false });
                p.WaitForExit();
                string output = p.StandardOutput.ReadToEnd();
                if (!output.Contains($"{vers}.0."))
                {
                    void Warn()
                    {
                        Logs.Warning($"You do not seem to have DotNET {vers} installed - this will be required in a future version of StableSwarmUI.");
                        Logs.Warning($"Please install DotNET SDK {vers}.0 from https://dotnet.microsoft.com/en-us/download/dotnet/{vers}.0");
                    }
                    DotNetVersMissing = vers;
                    Warn();
                    Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(_ => Warn());
                }
            }
            catch (Exception ex)
            {
                Logs.Debug($"Failed to check dotnet version: {ex}");
            }
        });
    }
}
