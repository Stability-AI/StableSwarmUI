using Newtonsoft.Json.Linq;
using StableUI.Core;
using StableUI.Backends;
using StableUI.Utils;

namespace StableUI.WebAPI;

/// <summary>Internal helper for all the basic API routes.</summary>
public static class BasicAPIFeatures
{
    /// <summary>Called by <see cref="Program"/> to register the core API calls.</summary>
    public static void Register()
    {
        API.RegisterAPICall(typeof(BasicAPIFeatures), nameof(GenerateText2Image));
    }

#pragma warning disable CS1998 // "CS1998 Async method lacks 'await' operators and will run synchronously"

    /// <summary>API route to generate an image.</summary>
    public static async Task<JObject> GenerateText2Image(string prompt, string negative_prompt = "", int images = 1, long seed = -1, int steps = 20, double cfg_scale = 7, int width = 512, int height = 512)
    {
        T2IBackendAccess backend;
        try
        {
            backend = Program.Backends.GetNextT2IBackend(TimeSpan.FromMinutes(2)); // TODO: Max timespan configurable
        }
        catch (InvalidOperationException ex)
        {
            return new JObject() { ["error"] = $"Invalid operation: {ex.Message}" };
        }
        catch (TimeoutException)
        {
            return new JObject() { ["error"] = "Timeout! All backends are occupied with other tasks." };
        }
        List<Image> allOutputs = new();
        if (seed == -1)
        {
            seed = Random.Shared.Next(int.MaxValue);
        }
        for (int i = 0; i < images; i++)
        {
            using (backend)
            {
                Image[] outputs = await backend.Backend.Generate(prompt, negative_prompt, seed + i, steps, width, height, cfg_scale);
                allOutputs.AddRange(outputs);
            }
        }
        return new JObject() { ["images"] = JToken.FromObject(allOutputs.Select(i => i.AsBase64).ToList()) };
    }
}
