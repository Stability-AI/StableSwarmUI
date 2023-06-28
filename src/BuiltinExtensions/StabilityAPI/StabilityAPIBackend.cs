
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
using static StableUI.Builtin_StabilityAPIExtension.StabilityAPIBackend;

namespace StableUI.Builtin_StabilityAPIExtension;

public class StabilityAPIBackend : AbstractT2IBackend<StabilityAPIBackendSettings>
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

    public override bool DoesProvideFeature(string feature)
    {
        return true;
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
        return JObject.Parse(data);
    }

    public async Task<JObject> Post(string url, JObject data)
    {
        return JObject.Parse(await (await WebClient.PostAsync($"{Settings.Endpoint}/{url}", Utilities.JSONContent(data))).Content.ReadAsStringAsync());
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

    public override async Task<Image[]> Generate(T2IParams user_input)
    {
        if (user_input.InitImage is not null)
        {
            // TODO: img2img
        }
        JArray prompts = new();
        if (!string.IsNullOrWhiteSpace(user_input.Prompt))
        {
            prompts.Add(new JObject()
            {
                ["text"] = user_input.Prompt,
                ["weight"] = 1
            });
        }
        if (!string.IsNullOrWhiteSpace(user_input.NegativePrompt))
        {
            prompts.Add(new JObject()
            {
                ["text"] = user_input.NegativePrompt,
                ["weight"] = -1
            });
        }
        JObject obj = new()
        {
            ["cfg_scale"] = user_input.CFGScale,
            ["height"] = user_input.Height,
            ["width"] = user_input.Width,
            ["samples"] = 1,
            ["steps"] = user_input.Steps,
            ["sampler"] = "K_EULER", // TODO: "DDIM" "DDPM" "K_DPMPP_2M" "K_DPMPP_2S_ANCESTRAL" "K_DPM_2" "K_DPM_2_ANCESTRAL" "K_EULER" "K_EULER_ANCESTRAL" "K_HEUN" "K_LMS"
            ["text_prompts"] = prompts,
            ["seed"] = user_input.Seed
        };
        string engine = user_input.OtherParams.GetValueOrDefault("sapi_engine", "stable-diffusion-v1-5").ToString();
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
}
