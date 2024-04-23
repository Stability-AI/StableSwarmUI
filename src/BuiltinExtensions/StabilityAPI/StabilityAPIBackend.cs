
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
using System.Net.Http.Headers;

namespace StableSwarmUI.Builtin_StabilityAPIExtension;

public class StabilityAPIBackend : AbstractT2IBackend
{
    public class StabilityAPIBackendSettings : AutoConfiguration
    {
        [ConfigComment("The endpoint route for the API, normally do not change this.")]
        public string Endpoint = "https://api.stability.ai/v2beta";

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
        WebClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // await UpdateBalance();
        // if (Credits == -1)
        // {
        //     Logs.Warning($"StabilityAPI Backend init failed.");
        //     Status = BackendStatus.DISABLED;
        //     return;
        // }
        // await RefreshEngines();
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

    public async Task<JObject> PostFormData(string url, JObject obj)
    {
        using (var formData = new MultipartFormDataContent())
        {
            // Add data to the form, ensuring each part has a 'name' attribute.
            foreach (var property in obj.Properties())
            {
                formData.Add(new StringContent(property.Value.ToString()), "\"" + property.Name + "\"");
            }

            HttpResponseMessage response = await WebClient.PostAsync(url, formData);
            string responseContent = await response.Content.ReadAsStringAsync();
            return JObject.Parse(responseContent);
        }
    }


    public override async Task<Image[]> Generate(T2IParamInput user_input)
    {
        user_input.ProcessPromptEmbeds(x => throw new InvalidDataException("Cannot use embeddings with StabilityAPI")); 
        if (user_input.TryGet(T2IParamTypes.InitImage, out Image initImg))
        {
            // TODO: img2img
        }
        if (string.IsNullOrEmpty(user_input.Get(T2IParamTypes.Prompt)))
        {
            throw new InvalidDataException("Invalid StabilityAPI generation input: missing prompt!");
        }
        // T2IModel model = user_input.Get(T2IParamTypes.Model);
        string engine = user_input.Get(StabilityAPIExtension.EngineParam);
        Console.WriteLine($"Using engine: {engine}");
        JObject obj = new()
        {
            ["prompt"] = user_input.Get(T2IParamTypes.Prompt),
            // ["negative_prompt"] = user_input.Get(T2IParamTypes.NegativePrompt), // Does not work on sd3-turbo
            ["model"] = engine,
            ["seed"] = user_input.Get(T2IParamTypes.Seed)
        };
        // TODO: Model tracking.
        JObject response = null;
        try
        {
            response = await PostFormData($"{Settings.Endpoint}/stable-image/generate/core", obj);
            List<Image> images = [];
            images.Add(new(response["image"].ToString(), Image.ImageType.IMAGE, "png"));
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
