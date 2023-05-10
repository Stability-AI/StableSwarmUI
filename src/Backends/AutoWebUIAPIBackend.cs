using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableUI.DataHolders;
using StableUI.Utils;
using static StableUI.Backends.AutoWebUIAPIBackend;

namespace StableUI.Backends;

/// <summary>T2I Backend using the Automatic1111/Stable-Diffusion-WebUI API.</summary>
public class AutoWebUIAPIBackend : AbstractT2IBackend<AutoWebUIAPISettings>
{
    public class AutoWebUIAPISettings : AutoConfiguration
    {
        /// <summary>Base web address of the auto webui instance.</summary>
        [SuggestionPlaceholder(Text = "WebUI's address...")]
        [ConfigComment("The address of the WebUI, eg 'http://localhost:7860'.")]
        public string Address = "";
    }

    /// <summary>Internal HTTP handler.</summary>
    public HttpClient HttpClient = new();

    public AutoWebUIAPIBackend()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"StableUI/{Utilities.Version}");
    }

    public override void Init()
    {
        IsValid = !string.IsNullOrWhiteSpace(Settings.Address);
        // TODO: Validate the server is alive.
    }

    public override void Shutdown()
    {
        IsValid = false;
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
        HttpResponseMessage response = await HttpClient.PostAsync($"{Settings.Address}/sdapi/v1/{url}", new StringContent(payload.ToString(Formatting.None), StringConversionHelper.UTF8Encoding, "application/json"));
        string content = await response.Content.ReadAsStringAsync();
        return JObject.Parse(content);
    }
}
