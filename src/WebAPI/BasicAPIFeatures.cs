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
    public static async Task<JObject> GenerateText2Image(string prompt, string negative_prompt, long seed, int steps = 20, int width = 512, int height = 512)
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
        using (backend)
        {
            Image[] images = await backend.Backend.Generate(prompt, negative_prompt, seed, steps, width, height);
            return new JObject() { ["images"] = JToken.FromObject(images.Select(i => i.AsBase64).ToList()) };
        }
    }
}
