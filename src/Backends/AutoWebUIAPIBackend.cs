using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableUI.DataHolders;
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

    public override async Task<Image[]> Generate(T2IParams user_input)
    {
        JObject result = await Send("txt2img", new JObject()
        {
            ["prompt"] = user_input.Prompt,
            ["negative_prompt"] = user_input.NegativePrompt,
            ["seed"] = user_input.Seed,
            ["steps"] = user_input.Steps,
            ["width"] = user_input.Width,
            ["height"] = user_input.Height,
            ["cfg_scale"] = user_input.CFGScale
        });
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
