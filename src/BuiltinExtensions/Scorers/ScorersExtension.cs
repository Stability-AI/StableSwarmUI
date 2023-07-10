
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableUI.Backends;
using StableUI.Core;
using StableUI.Text2Image;
using StableUI.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace StableUI.Builtin_ScorersExtension;

public class ScorersExtension : Extension
{
    public static string[] ScoringEngines = new string[] { "pickscore", "schuhmann_clip_plus_mlp" };

    public override void OnInit()
    {
        T2IEngine.PostGenerateEvent += PostGenEvent;
        T2IEngine.PostBatchEvent += PostBatchEvent;
        T2IParamGroup scoreGroup = new("Scoring", Toggles: true, Open: false);
        T2IParamTypes.Register(new("Automatic Scorer", "Scoring engine(s) to use when scoring this image. Multiple scorers can be used via comma-separated list, and will be averaged together. Scores are saved in image metadata.",
                       T2IParamDataType.TEXT, "schuhmann_clip_plus_mlp", (s, p) => p.OtherParams["scoring_engines"] = s, Group: scoreGroup, GetValues: (_) => ScoringEngines.ToList()
                       // TODO: TYPE MULTISELECT
                       ));
        T2IParamTypes.Register(new("Score Must Exceed", "Only keep images with a generated score above this minimum.",
                       T2IParamDataType.DECIMAL, "0.5", (s, p) => p.OtherParams["score_minimum"] = float.Parse(s), Min: 0, Max: 1, Step: 0.1, Toggleable: true, Group: scoreGroup, Examples: new[] { "0.25", "0.5", "0.75", "0.9" }
                       ));
        T2IParamTypes.Register(new("Take Best N Score", "Only keep the best *this many* images in a batch based on scoring."
                        + "\n(For example, if batch size = 8, and this value = 2, then 8 images will generate and will be scored, and the 2 best will be kept and the other 6 discarded.)",
                       T2IParamDataType.INTEGER, "1", (s, p) => p.OtherParams["score_take_best_n"] = int.Parse(s), Min: 1, Max: 100, Step: 1, Toggleable: true, Group: scoreGroup, Examples: new[] { "1", "2", "3" }
                       ));
    }

    public override void OnShutdown()
    {
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

    public HttpClient WebClient;

    public int Port;

    public Process RunningProcess;

    public volatile BackendStatus Status = BackendStatus.DISABLED;

    public LockObject InitLocker = new();

    /// <summary>Does not return until the backend process is ready.</summary>
    public void EnsureActive()
    {
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
            WebClient = NetworkBackendUtils.MakeHttpClient();
            async Task<bool> Check(bool _)
            {
                try
                {
                    if (await DoPost("API/Ping", new()) != null)
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
            NetworkBackendUtils.DoSelfStart(FilePath + "scorer_engine.py", "ScorersExtension", 0, "{PORT}", s => Status = s, Check, (p, r) => { Port = p; RunningProcess = r; }, () => Status).Wait();
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
        if (!p.UserInput.OtherParams.TryGetValue("scoring_engines", out object scorers))
        {
            return;
        }
        float scoreAccum = 0;
        string[] scorerNames = scorers.ToString().Split(',').Select(s => s.Trim().ToLowerFast()).ToArray();
        Dictionary<string, object> scores = new();
        foreach (string scorer in scorerNames)
        {
            if (!ScoringEngines.Contains(scorer))
            {
                throw new InvalidDataException($"Scorer {scorer} does not exist.");
            }
            float score = DoScore(p.Image, p.UserInput.Prompt, scorer).Result;
            scores[scorer] = score;
            scoreAccum += score;
        }
        float averageScore = scoreAccum / scorerNames.Length;
        scores["average"] = averageScore;
        p.ExtraMetadata["scoring"] = scores;
        if (p.UserInput.OtherParams.TryGetValue("score_minimum", out object scoreMin) && float.TryParse(scoreMin.ToString(), out float scoreMinimum))
        {
            if (averageScore < scoreMinimum)
            {
                p.RefuseImage();
            }
        }
    }

    public void PostBatchEvent(T2IEngine.PostBatchEventParams p)
    {
        if (!p.UserInput.OtherParams.TryGetValue("score_take_best_n", out object bestNObj) || bestNObj is not int bestN)
        {
            return;
        }
        if (p.Images.Length <= bestN)
        {
            Logs.Debug($"Scorers: Limited to {bestN} images but only found {p.Images.Length} to scan, so ignoring");
            return;
        }
        float[] scores = p.Images.Select(i => i?.Image?.GetSUIMetadata()?["scoring"]?["average"]?.Value<float>() ?? 0).ToArray();
        float[] sorted = scores.OrderDescending().ToArray();
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
