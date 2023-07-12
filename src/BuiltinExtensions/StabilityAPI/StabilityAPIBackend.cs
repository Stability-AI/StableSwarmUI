
using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableUI.Backends;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Text2Image;
using StableUI.Utils;
using System.IO;
using System.Net.Http;

namespace StableUI.Builtin_StabilityAPIExtension;

public class StabilityAPIBackend : AbstractT2IBackend
{
    public class StabilityAPIBackendSettings : AutoConfiguration
    {
        [ConfigComment("The endpoint route for the API, normally do not change this.")]
        public string Endpoint = "https://api.stability.ai/v1";

        [ConfigComment("The name of a file under your 'Data/' directory that is a plaintext file containing the SAPI key.")]
        public string KeyFile = "sapi_key.dat";
    }

    private static string Key;

    public static double Credits = -1;

    public HttpClient WebClient;

    public StabilityAPIBackendSettings Settings => SettingsRaw as StabilityAPIBackendSettings;

    public override async Task Init()
    {
        string fn = $"{Program.ServerSettings.DataPath}/{Settings.KeyFile}";
        if (string.IsNullOrWhiteSpace(fn) || fn.Contains("..") || !File.Exists(fn))
        {
            Logs.Warning($"Refusing to initialize SAPI backend because {fn} does not exist or is invalid.");
            Status = BackendStatus.DISABLED;
            return;
        }
        Key = File.ReadAllText(fn).Trim();
        WebClient = NetworkBackendUtils.MakeHttpClient();
        WebClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {Key}");
        await UpdateBalance();
        if (Credits == -1)
        {
            Logs.Warning($"SAPI Backend init failed.");
            Status = BackendStatus.DISABLED;
            return;
        }
        await RefreshEngines();
        Status = BackendStatus.RUNNING;
    }

    public override Task<bool> LoadModel(T2IModel model)
    {
        CurrentModelName = model.Name;
        // Nothing to do.
        return Task.FromResult(true);
    }

    public override Task Shutdown()
    {
        // Nothing to do.
        return Task.CompletedTask;
    }

    public async Task<JObject> Get(string url)
    {
        string data = await (await WebClient.GetAsync($"{Settings.Endpoint}/{url}")).Content.ReadAsStringAsync();
        if (data.StartsWith('['))
        {
            data = "{\"data\":" + data + "}";
        }
        return data.ParseToJson();
    }

    public async Task<JObject> Post(string url, JObject data)
    {
        return (await (await WebClient.PostAsync($"{Settings.Endpoint}/{url}", Utilities.JSONContent(data))).Content.ReadAsStringAsync()).ParseToJson();
    }

    public async Task RefreshEngines()
    {
        JObject engines = await Get("engines/list");
        List<string> engineIds = engines["data"].Select(o => o["id"].ToString()).ToList();
        Logs.Info($"Engines: {engines}");
        lock (StabilityAPIExtension.TrackerLock)
        {
            foreach (string eng in engineIds)
            {
                if (!StabilityAPIExtension.Engines.Contains(eng))
                {
                    StabilityAPIExtension.Engines.Add(eng);
                }
            }
        }
    }

    public async Task UpdateBalance()
    {
        double _cred = (double)(await Get("user/balance"))["credits"];
        lock (StabilityAPIExtension.TrackerLock)
        {
            Credits = _cred;
        }
        Logs.Info($"SAPI balance: {Credits}");
    }

    public override async Task<Image[]> Generate(T2IParamInput user_input)
    {
        if (user_input.TryGet(T2IParamTypes.InitImage, out Image initImg))
        {
            // TODO: img2img
        }
        JArray prompts = new();
        if (!string.IsNullOrWhiteSpace(user_input.Get(T2IParamTypes.Prompt)))
        {
            prompts.Add(new JObject()
            {
                ["text"] = user_input.Get(T2IParamTypes.Prompt),
                ["weight"] = 1
            });
        }
        if (!string.IsNullOrWhiteSpace(user_input.Get(T2IParamTypes.NegativePrompt)))
        {
            prompts.Add(new JObject()
            {
                ["text"] = user_input.Get(T2IParamTypes.NegativePrompt),
                ["weight"] = -1
            });
        }
        JObject obj = new()
        {
            ["cfg_scale"] = user_input.Get(T2IParamTypes.CFGScale),
            ["height"] = user_input.Get(T2IParamTypes.Height),
            ["width"] = user_input.Get(T2IParamTypes.Width),
            ["samples"] = 1,
            ["steps"] = user_input.Get(T2IParamTypes.Steps),
            ["sampler"] = user_input.Get(StabilityAPIExtension.SamplerParam) ?? "K_EULER",
            ["text_prompts"] = prompts,
            ["seed"] = user_input.Get(T2IParamTypes.Seed)
        };
        string engine = user_input.Get(StabilityAPIExtension.EngineParam) ?? "stable-diffusion-v1-5";
        // TODO: Model tracking.
        JObject response = await Post($"generation/{engine}/text-to-image", obj);
        List<Image> images = new();
        foreach (JObject img in response["artifacts"].Cast<JObject>())
        {
            if (img["finishReason"].ToString() == "ERROR")
            {
                Logs.Error($"SAPI returned error for request.");
            }
            else
            {
                images.Add(new(img["base64"].ToString()));
            }
        }
        _ = Task.Run(() => UpdateBalance().Wait());
        return images.ToArray();
    }

    public override IEnumerable<string> SupportedFeatures => StabilityAPIExtension.FeaturesSupported;
}
