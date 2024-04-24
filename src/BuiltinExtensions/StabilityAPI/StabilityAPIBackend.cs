
using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System;
using System.IO;
using System.Net.Http;

namespace StableSwarmUI.Builtin_StabilityAPIExtension;

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
        string fn = Utilities.CombinePathWithAbsolute(Program.ServerSettings.Paths.DataPath, Settings.KeyFile);
        if (string.IsNullOrWhiteSpace(fn) || fn.Contains("..") || !File.Exists(fn))
        {
            Logs.Warning($"Refusing to initialize StabilityAPI backend because {fn} does not exist or is invalid.");
            Status = BackendStatus.DISABLED;
            return;
        }
        Key = File.ReadAllText(fn).Trim();
        WebClient = NetworkBackendUtils.MakeHttpClient();
        WebClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {Key}");
        await UpdateBalance();
        if (Credits == -1)
        {
            Logs.Warning($"StabilityAPI Backend init failed.");
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
        NetworkBackendUtils.ClearOldHttpClient(WebClient);
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

    public async Task RefreshEngines()
    {
        JObject engines = await Get("engines/list");
        List<string> engineIds = engines["data"].Select(o => o["id"].ToString()).ToList();
        Logs.Debug($"Engines: {engines}");
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
        JObject response = await Get("user/balance");
        if (!response.TryGetValue("credits", out JToken creditTok))
        {
            Logs.Error($"StabilityAPI gave unexpected response to balance check: {response}");
            return;
        }
        double _cred = (double)creditTok;
        lock (StabilityAPIExtension.TrackerLock)
        {
            Credits = _cred;
        }
        Logs.Info($"StabilityAPI balance: {Credits}");
    }

    public override async Task<Image[]> Generate(T2IParamInput user_input)
    {
        user_input.ProcessPromptEmbeds(x => throw new InvalidDataException("Cannot use embeddings with StabilityAPI")); 
        if (user_input.TryGet(T2IParamTypes.InitImage, out Image initImg))
        {
            // TODO: img2img
        }
        JArray prompts = [];
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
        if (prompts.IsEmpty())
        {
            throw new InvalidDataException($"Invalid StabilityAPI generation input: missing prompt!");
        }
        JObject obj = new()
        {
            ["cfg_scale"] = user_input.Get(T2IParamTypes.CFGScale),
            ["height"] = user_input.GetImageHeight(),
            ["width"] = user_input.Get(T2IParamTypes.Width),
            ["samples"] = 1,
            ["steps"] = user_input.Get(T2IParamTypes.Steps),
            ["sampler"] = user_input.Get(StabilityAPIExtension.SamplerParam) ?? "K_EULER",
            ["text_prompts"] = prompts,
            ["seed"] = user_input.Get(T2IParamTypes.Seed)
        };
        T2IModel model = user_input.Get(T2IParamTypes.Model);
        string sapiEngineForModel = model.ModelClass?.ID switch
        {
            "stable-diffusion-xl-v1-base" or "stable-diffusion-xl-v1-refiner" => "stable-diffusion-xl-1024-v1-0",
            "stable-diffusion-v2-inpainting" => "stable-inpainting-512-v2-0",
            "stable-diffusion-v2-depth" => "stable-diffusion-depth-v2-0",
            "stable-diffusion-v1-inpainting" => "stable-inpainting-v1-0",
            "stable-diffusion-v2-768-v" => "stable-diffusion-768-v2-1",
            "stable-diffusion-v2-512" => "stable-diffusion-512-v2-1",
            _ => "stable-diffusion-v1-5"
        };
        string engine = user_input.Get(StabilityAPIExtension.EngineParam, sapiEngineForModel);
        // TODO: Model tracking.
        JObject response = null;
        try
        {
            response = await WebClient.PostJson($"{Settings.Endpoint}/generation/{engine}/text-to-image", obj);
            if (!response.ContainsKey("artifacts") && response.TryGetValue("message", out JToken message))
            {
                throw new InvalidDataException($"StabilityAPI refused to generate: {message}");
            }
            List<Image> images = [];
            foreach (JObject img in response["artifacts"].Cast<JObject>())
            {
                if (img["finishReason"].ToString() == "ERROR")
                {
                    Logs.Error($"StabilityAPI returned error for request.");
                }
                else
                {
                    images.Add(new(img["base64"].ToString(), Image.ImageType.IMAGE, "png"));
                }
            }
            _ = Utilities.RunCheckedTask(() => UpdateBalance().Wait());
            return [.. images];
        }
        catch (Exception ex)
        {
            Logs.Error($"StabilityAPI request failed: {ex.GetType().Name}: {ex.Message}");
            Logs.Debug($"Raw StabilityAPI request input was: {obj}");
            Logs.Debug($"raw StabilityAPI response for above error was: {response}");
            throw;
        }
    }

    public override IEnumerable<string> SupportedFeatures => StabilityAPIExtension.FeaturesSupported;
}
