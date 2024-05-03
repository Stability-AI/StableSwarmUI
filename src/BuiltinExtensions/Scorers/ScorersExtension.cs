
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace StableSwarmUI.Builtin_ScorersExtension;

public class ScorersExtension : Extension
{
    public static string[] ScoringEngines = ["pickscore", "schuhmann_clip_plus_mlp"];

    public static T2IRegisteredParam<List<string>> AutomaticScorer;

    public static T2IRegisteredParam<double> ScoreMustExceed;

    public static T2IRegisteredParam<int> TakeBestNScore;

    public static Action ShutdownEvent;

    public override void OnInit()
    {
        T2IEngine.PostGenerateEvent += PostGenEvent;
        T2IEngine.PostBatchEvent += PostBatchEvent;
        T2IParamGroup scoreGroup = new("Scoring", Toggles: true, IsAdvanced: true, Open: false);
        AutomaticScorer = T2IParamTypes.Register<List<string>>(new("Automatic Scorer", "Scoring engine(s) to use when scoring this image. Multiple scorers can be used and will be averaged together. Scores are saved in image metadata.",
                       "schuhmann_clip_plus_mlp", Group: scoreGroup, GetValues: (_) => [.. ScoringEngines]
                       ));
        ScoreMustExceed = T2IParamTypes.Register<double>(new("Score Must Exceed", "Only keep images with a generated score above this minimum.",
                       "0.5", Min: 0, Max: 1, Step: 0.1, Toggleable: true, Group: scoreGroup, Examples: ["0.25", "0.5", "0.75", "0.9"]
                       ));
        TakeBestNScore = T2IParamTypes.Register<int>(new("Take Best N Score", "Only keep the best *this many* images in a batch based on scoring."
                        + "\n(For example, if batch size = 8, and this value = 2, then 8 images will generate and will be scored, and the 2 best will be kept and the other 6 discarded.)",
                       "1", Min: 1, Max: 100, Step: 1, Toggleable: true, Group: scoreGroup, Examples: ["1", "2", "3"]
                       ));
    }

    public override void OnShutdown()
    {
        ShutdownEvent?.Invoke();
        T2IEngine.PostGenerateEvent -= PostGenEvent;
        T2IEngine.PostBatchEvent -= PostBatchEvent;
        if (RunningProcess is not null)
        {
            if (!RunningProcess.HasExited)
            {
                RunningProcess.Kill();
            }
            RunningProcess = null;
        }
    }

    public static HttpClient WebClient;

    public int Port;

    public Process RunningProcess;

    public volatile BackendStatus Status = BackendStatus.DISABLED;

    public LockObject InitLocker = new();

    /// <summary>Does not return until the backend process is ready.</summary>
    public void EnsureActive()
    {
        if (!Directory.Exists($"{FilePath}/venv"))
        {
            throw new InvalidOperationException("Scoring parameter is enabled, but Scorers extension is not installed.\nThe scorers extension is experimental, you'll probably want to just uncheck the parameter.");
        }
        while (Status == BackendStatus.LOADING)
        {
            Task.Delay(TimeSpan.FromSeconds(0.5)).Wait(Program.GlobalProgramCancel);
        }
        lock (InitLocker)
        {
            if (Status == BackendStatus.RUNNING || Program.GlobalProgramCancel.IsCancellationRequested)
            {
                return;
            }
            WebClient ??= NetworkBackendUtils.MakeHttpClient();
            async Task<bool> Check(bool _)
            {
                try
                {
                    if (await DoPost("API/Ping", []) != null)
                    {
                        Status = BackendStatus.RUNNING;
                    }
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            NetworkBackendUtils.DoSelfStart(FilePath + "scorer_engine.py", "ScorersExtension", "scorersextension", 0, "{PORT}", s => Status = s, Check, (p, r) => { Port = p; RunningProcess = r; }, () => Status, a => ShutdownEvent += a).Wait();
        }
    }

    public async Task<JObject> DoPost(string url, JObject data)
    {
        return (await (await WebClient.PostAsync($"http://localhost:{Port}/{url}", Utilities.JSONContent(data))).Content.ReadAsStringAsync()).ParseToJson();
    }

    public async Task<float> DoScore(Image image, string prompt, string scorer)
    {
        EnsureActive();
        JObject result = await DoPost("API/DoScore", new() { ["prompt"] = prompt, ["image"] = image.AsBase64, ["scorer"] = scorer });
        if (result.TryGetValue("log", out JToken ltext))
        {
            Logs.Debug($"ScorerExtension log: {ltext}");
        }
        if (result.TryGetValue("error", out JToken etext))
        {
            Logs.Error($"ScorerExtension error: {etext}");
        }
        return (float)result["result"];
    }

    public void PostGenEvent(T2IEngine.PostGenerationEventParams p)
    {
        if (!p.UserInput.TryGet(AutomaticScorer, out List<string> scorers) || scorers.IsEmpty())
        {
            return;
        }
        float scoreAccum = 0;
        Dictionary<string, object> scores = [];
        foreach (string scorer in scorers)
        {
            if (!ScoringEngines.Contains(scorer))
            {
                throw new InvalidDataException($"Scorer {scorer} does not exist.");
            }
            float score = DoScore(p.Image, p.UserInput.Get(T2IParamTypes.Prompt), scorer).Result;
            scores[scorer] = score;
            scoreAccum += score;
        }
        float averageScore = scoreAccum / scorers.Count;
        scores["average"] = averageScore;
        p.UserInput.ExtraMeta["scoring"] = scores;
        if (p.UserInput.TryGet(ScoreMustExceed, out double scoreMin))
        {
            if (averageScore < scoreMin)
            {
                p.RefuseImage();
            }
        }
    }

    public void PostBatchEvent(T2IEngine.PostBatchEventParams p)
    {
        if (!p.UserInput.TryGet(TakeBestNScore, out int bestN))
        {
            return;
        }
        if (p.Images.Length <= bestN)
        {
            Logs.Debug($"Scorers: Limited to {bestN} images but only found {p.Images.Length} to scan, so ignoring");
            return;
        }
        float[] scores = p.Images.Select(i => i?.Img?.GetSUIMetadata()?["scoring"]?["average"]?.Value<float>() ?? 0).ToArray();
        float[] sorted = [.. scores.OrderDescending()];
        float cutoff = sorted[bestN - 1];
        Logs.Debug($"Scorers: will cutoff to {bestN} images with score {cutoff} or above");
        for (int i = 0; i < p.Images.Length; i++)
        {
            if (scores[i] < cutoff)
            {
                p.Images[i].RefuseImage();
            }
        }
    }
}
