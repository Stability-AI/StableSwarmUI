using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableUI.Utils;
using System.Net.Http.Headers;

namespace StableUI.Backends;

/// <summary>T2I Backend using the Automatic1111/Stable-Diffusion-WebUI API.</summary>
public class AutoWebUIAPIBackend : AbstractT2IBackend
{
    /// <summary>Base web address of the auto webui instance.</summary>
    public string Address;

    /// <summary>
    /// 
    /// </summary>
    public HttpClient HttpClient = new();

    public AutoWebUIAPIBackend(string _address)
    {
        Address = _address;
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"StableUI/{Utilities.Version}");
    }

    public override void Init()
    {
        // TODO: Validate the server is alive.
    }

    public override void Shutdown()
    {
        // Nothing to do, not our server.
    }

    public override async Task<Image[]> Generate(string prompt, string negativePrompt, long seed, int steps, int width, int height)
    {
        JObject result = await Send("txt2img", new JObject() { ["prompt"] = prompt, ["negative_prompt"] = negativePrompt, ["seed"] = seed, ["steps"] = steps, ["width"] = width, ["height"] = height });
        // TODO: Error handlers
        return result["images"].Select(i => new Image((string)i)).ToArray();
    }

    public async Task<JObject> Send(string url, JObject payload)
    {
        HttpResponseMessage response = await HttpClient.PostAsync($"{Address}/sdapi/v1/{url}", new StringContent(payload.ToString(Formatting.None), StringConversionHelper.UTF8Encoding, "application/json"));
        string content = await response.Content.ReadAsStringAsync();
        return JObject.Parse(content);
    }
}
