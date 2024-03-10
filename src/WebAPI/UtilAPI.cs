using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using System.Diagnostics;
using System.IO;

namespace StableSwarmUI.WebAPI;

/// <summary>Central API registration class for utility APIs.</summary>
public static class UtilAPI
{
    public static void Register()
    {
        API.RegisterAPICall(CountTokens);
        API.RegisterAPICall(TokenizeInDetail);
        API.RegisterAPICall(Pickle2SafeTensor);
        API.RegisterAPICall(WipeMetadata);
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

    /// <summary>API route to count the CLIP-like tokens in a given text prompt.</summary>
    public static async Task<JObject> CountTokens(string text, bool skipPromptSyntax = false, string tokenset = "clip", bool weighting = true)
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
        if (error != null)
        {
            return error;
        }
        CliplikeTokenizer.Token[] tokens = weighting ? tokenizer.EncodeWithWeighting(text) : tokenizer.Encode(text);
        return new JObject() { ["count"] = tokens.Length };
    }

    /// <summary>API route to tokenize some prompt text and get thorough detail about it.</summary>
    public static async Task<JObject> TokenizeInDetail(string text, string tokenset = "clip", bool weighting = true)
    {
        (JObject error, CliplikeTokenizer tokenizer) = GetTokenizerForAPI(text, tokenset);
        if (error != null)
        {
            return error;
        }
        CliplikeTokenizer.Token[] tokens = weighting ? tokenizer.EncodeWithWeighting(text) : tokenizer.Encode(text);
        return new JObject()
        {
            ["tokens"] = new JArray(tokens.Select(t => new JObject() { ["id"] = t.ID, ["weight"] = t.Weight, ["text"] = tokenizer.Tokens[t.ID] }).ToArray())
        };
    }

    /// <summary>API route to trigger bulk conversion of models from pickle format to safetensors.</summary>
    public static async Task<JObject> Pickle2SafeTensor(string type, bool fp16)
    {
        if (!Program.T2IModelSets.TryGetValue(type, out T2IModelHandler models))
        {
            return new JObject() { ["error"] = $"Invalid type '{type}'." };
        }
        Process p = PythonLaunchHelper.LaunchGeneric("launchtools/pickle-to-safetensors.py", true, [models.FolderPath, fp16 ? "true" : "false"]);
        await p.WaitForExitAsync(Program.GlobalProgramCancel);
        return new JObject() { ["success"] = true };
    }

    /// <summary>API route to trigger a mass metadata reset.</summary>
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
