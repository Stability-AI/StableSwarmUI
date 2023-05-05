using Newtonsoft.Json.Linq;
using StableUI.Core;

namespace StableUI.WebAPI;

/// <summary>Internal helper for all the basic API routes.</summary>
public static class BasicAPIFeatures
{
    /// <summary>Called by <see cref="Program"/> to register the core API calls.</summary>
    public static void Register()
    {
        API.RegisterAPICall(new API.APICall("Generate", Generate));
    }

#pragma warning disable CS1998 // "CS1998 Async method lacks 'await' operators and will run synchronously"

    /// <summary>API route to generate an image.</summary>
    public static async Task<JObject> Generate(HttpContext context, JObject input)
    {
        return new JObject() { ["status"] = "test" };
    }
}
