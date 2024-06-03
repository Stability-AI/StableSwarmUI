using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.Diagnostics;
using System.IO;

namespace StableSwarmUI.WebAPI;

[API.APIClass("General utility API routes.")]
public static class UtilAPI
{
    public static void Register()
    {
        API.RegisterAPICall(CountTokens);
        API.RegisterAPICall(TokenizeInDetail);
        API.RegisterAPICall(Pickle2SafeTensor, true);
        API.RegisterAPICall(WipeMetadata, true);
    }

    public static ConcurrentDictionary<string, CliplikeTokenizer> Tokenizers = new();

    private static (JObject, CliplikeTokenizer) GetTokenizerForAPI(string text, string tokenset)
    {
        if (text.Length > 100 * 1024)
        {
            return (new JObject() { ["error"] = "Text too long, refused." }, null);
        }
        tokenset = Utilities.FilePathForbidden.TrimToNonMatches(tokenset);
        if (tokenset.Contains('/') || tokenset.Contains('.') || tokenset.Trim() == "" || tokenset.Length > 128)
        {
            return (new JObject() { ["error"] = "Invalid tokenset (refused characters or format), refused." }, null);
        }
        try
        {
            CliplikeTokenizer tokenizer = Tokenizers.GetOrCreate(tokenset, () =>
            {
                string fullPath = $"src/srcdata/Tokensets/{tokenset}.txt.gz";
                if (!File.Exists(fullPath))
                {
                    throw new InvalidOperationException($"Tokenset '{tokenset}' does not exist.");
                }
                CliplikeTokenizer tokenizer = new();
                tokenizer.Load(fullPath);
                return tokenizer;
            });
            return (null, tokenizer);
        }
        catch (InvalidOperationException ex)
        {
            return (new JObject() { ["error"] = ex.Message },  null);
        }
    }

    private static readonly string[] SkippablePromptSyntax = ["segment", "object", "region", "clear"];

    [API.APIDescription("Count the CLIP-like tokens in a given text prompt.", "\"count\": 0")]
    public static async Task<JObject> CountTokens(
        [API.APIParameter("The text to tokenize.")] string text,
        [API.APIParameter("If false, processing prompt syntax (things like `<random:`). If true, don't process that.")] bool skipPromptSyntax = false,
        [API.APIParameter("What tokenization set to use.")] string tokenset = "clip",
        [API.APIParameter("If true, process weighting (like `(word:1.5)`). If false, don't process that.")] bool weighting = true)
    {
        if (skipPromptSyntax)
        {
            foreach (string str in SkippablePromptSyntax)
            {
                int skippable = text.IndexOf($"<{str}:");
                if (skippable != -1)
                {
                    text = text[..skippable];
                }
            }
            text = T2IParamInput.ProcessPromptLikeForLength(text);
        }
        (JObject error, CliplikeTokenizer tokenizer) = GetTokenizerForAPI(text, tokenset);
        if (error is not null)
        {
            return error;
        }
        if (!weighting)
        {
            CliplikeTokenizer.Token[] rawTokens = tokenizer.Encode(text);
            return new JObject() { ["count"] = rawTokens.Length };
        }
        string[] sections = text.Split("<break>");
        int biggest = sections.Select(text => tokenizer.EncodeWithWeighting(text).Length).Max();
        return new JObject() { ["count"] = biggest };
    }

    [API.APIDescription("Tokenize some prompt text and get thorough detail about it.",
        """
            "tokens":
            [
                {
                    "id": 123,
                    "weight": 1.0,
                    "text": "tok"
                }
            ]
        """)]
    public static async Task<JObject> TokenizeInDetail(
        [API.APIParameter("The text to tokenize.")] string text,
        [API.APIParameter("What tokenization set to use.")] string tokenset = "clip",
        [API.APIParameter("If true, process weighting (like `(word:1.5)`). If false, don't process that.")] bool weighting = true)
    {
        (JObject error, CliplikeTokenizer tokenizer) = GetTokenizerForAPI(text, tokenset);
        if (error is not null)
        {
            return error;
        }
        CliplikeTokenizer.Token[] tokens = weighting ? tokenizer.EncodeWithWeighting(text) : tokenizer.Encode(text);
        return new JObject()
        {
            ["tokens"] = new JArray(tokens.Select(t => new JObject() { ["id"] = t.ID, ["weight"] = t.Weight, ["text"] = tokenizer.Tokens[t.ID] }).ToArray())
        };
    }

    [API.APIDescription("Trigger bulk conversion of models from pickle format to safetensors.", "\"success\": true")]
    public static async Task<JObject> Pickle2SafeTensor(
        [API.APIParameter("What type of model to convert, eg `Stable-Diffusion`, `LoRA`, etc.")] string type,
        [API.APIParameter("If true, convert to fp16 while processing. If false, use original model's weight type.")] bool fp16)
    {
        if (!Program.T2IModelSets.TryGetValue(type, out T2IModelHandler models))
        {
            return new JObject() { ["error"] = $"Invalid type '{type}'." };
        }
        Process p = PythonLaunchHelper.LaunchGeneric("launchtools/pickle-to-safetensors.py", true, [models.FolderPaths[0], fp16 ? "true" : "false"]);
        await p.WaitForExitAsync(Program.GlobalProgramCancel);
        return new JObject() { ["success"] = true };
    }

    [API.APIDescription("Trigger a mass metadata reset.", "\"success\": true")]
    public static async Task<JObject> WipeMetadata()
    {
        foreach (T2IModelHandler handler in Program.T2IModelSets.Values)
        {
            handler.MassRemoveMetadata();
        }
        ImageMetadataTracker.MassRemoveMetadata();
        return new JObject() { ["success"] = true };
    }
}
