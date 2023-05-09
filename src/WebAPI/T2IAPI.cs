using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableUI.Backends;
using StableUI.Core;
using StableUI.Utils;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace StableUI.WebAPI;

/// <summary>Text-to-Image API routes</summary>
public static class T2IAPI
{
    public static void Register()
    {
        API.RegisterAPICall(GenerateText2Image);
        API.RegisterAPICall(GenerateText2ImageWS);
    }


    /// <summary>API route to generate images with WebSocket updates.</summary>
    public static async Task<JObject> GenerateText2ImageWS(WebSocket socket, string prompt, string negative_prompt = "", int images = 1, long seed = -1, int steps = 20, double cfg_scale = 7, int width = 512, int height = 512)
    {
        await foreach ((Image img, JObject err) in GenT2I_Internal(prompt, negative_prompt, images, seed, steps, cfg_scale, width, height))
        {
            if (img is not null)
            {
                await socket.SendJson(new JObject() { ["image"] = img.AsBase64 }, TimeSpan.FromMinutes(1)); // TODO: Configurable timeout
            }
            if (err is not null)
            {
                await socket.SendJson(err, TimeSpan.FromMinutes(1));
                break;
            }
        }
        return null;
    }

    /// <summary>API route to generate images directly as HTTP.</summary>
    public static async Task<JObject> GenerateText2Image(string prompt, string negative_prompt = "", int images = 1, long seed = -1, int steps = 20, double cfg_scale = 7, int width = 512, int height = 512)
    {
        List<Image> outputs = new();
        await foreach ((Image img, JObject err) in GenT2I_Internal(prompt, negative_prompt, images, seed, steps, cfg_scale, width, height))
        {
            if (img is not null)
            {
                outputs.Add(img);
            }
            if (err is not null)
            {
                return err;
            }
        }
        return new JObject(JToken.FromObject(outputs.Select(i => i.AsBase64).ToList()));
    }

    /// <summary>Internal route for generating images.</summary>
    public static async IAsyncEnumerable<(Image, JObject)> GenT2I_Internal(string prompt, string negative_prompt, int images, long seed, int steps, double cfg_scale, int width, int height)
    {
        ConcurrentQueue<Image> allOutputs = new();
        if (seed == -1)
        {
            seed = Random.Shared.Next(int.MaxValue);
        }
        JObject errorOut = null;
        List<Task> tasks = new();
        int max_degrees = 4; // TODO: Configure max degrees parallel based on user limit / global limit / backend count
        for (int i = 0; i < images; i++)
        {
            tasks.RemoveAll(t => t.IsCompleted);
            if (tasks.Count > max_degrees)
            {
                await Task.WhenAny(tasks);
            }
            if (Volatile.Read(ref errorOut) is not null)
            {
                yield return (null, errorOut);
                yield break;
            }
            while (allOutputs.TryDequeue(out Image output))
            {
                yield return (output, null);
            }
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                T2IBackendAccess backend;
                try
                {
                    backend = Program.Backends.GetNextT2IBackend(TimeSpan.FromMinutes(2)); // TODO: Max timespan configurable
                }
                catch (InvalidOperationException ex)
                {
                    Volatile.Write(ref errorOut, new JObject() { ["error"] = $"Invalid operation: {ex.Message}" });
                    return;
                }
                catch (TimeoutException)
                {
                    Volatile.Write(ref errorOut, new JObject() { ["error"] = "Timeout! All backends are occupied with other tasks." });
                    return;
                }
                if (Volatile.Read(ref errorOut) is not null)
                {
                    return;
                }
                using (backend)
                {
                    Image[] outputs = await backend.Backend.Generate(prompt, negative_prompt, seed + index, steps, width, height, cfg_scale);
                    foreach (Image image in outputs)
                    {
                        allOutputs.Enqueue(image);
                    }
                }
            }));
        }
        while (tasks.Any())
        {
            await Task.WhenAny(tasks);
            tasks.RemoveAll(t => t.IsCompleted);
            while (allOutputs.TryDequeue(out Image output))
            {
                yield return (output, null);
            }
        }
        errorOut = Volatile.Read(ref errorOut);
        if (errorOut is not null)
        {
            yield return (null, errorOut);
            yield break;
        }
    }
}
