
using FreneticUtilities.FreneticExtensions;
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
        T2IParamTypes.Register(new("Automatic Scorer", "Scoring engine(s) to use when scoring this image. Multiple scorers can be used via comma-separated list, and will be averaged together. Scores are saved in image metadata.",
                       T2IParamDataType.TEXT, "schuhmann_clip_plus_mlp", (s, p) => p.OtherParams["scoring_engines"] = s, Toggleable: true, Group: "Scoring", GetValues: (_) => ScoringEngines.ToList()
                       // TODO: TYPE MULTISELECT
                       ));
        T2IParamTypes.Register(new("Score Must Exceed", "Only keep images with a generated score above this minimum.",
                       T2IParamDataType.DECIMAL, "0.5", (s, p) => p.OtherParams["score_minimum"] = float.Parse(s), Toggleable: true, Group: "Scoring", Examples: new[] { "0.25", "0.5", "0.75", "0.9" }
                       ));
    }

    public override void OnShutdown()
    {
        T2IEngine.PostGenerateEvent -= PostGenEvent;
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

    public BackendStatus Status = BackendStatus.DISABLED;

    /// <summary>Does not return until the backend process is ready.</summary>
    public void EnsureActive()
    {
        while (Status == BackendStatus.LOADING)
        {
            Task.Delay(TimeSpan.FromSeconds(0.5)).Wait(Program.GlobalProgramCancel);
        }
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
}
