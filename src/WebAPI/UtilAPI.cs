using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Utils;
using System.IO;

namespace StableSwarmUI.WebAPI;

/// <summary>Central API registration class for utility APIs.</summary>
public static class UtilAPI
{
    public static void Register()
    {
        API.RegisterAPICall(CountTokens);
        API.RegisterAPICall(TokenizeInDetail);
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
            CliplikeTokenizer tokenizer = Tokenizers.GetOrAdd(tokenset, set =>
            {
                string fullPath = $"src/data/Tokensets/{set}.txt.gz";
                if (!File.Exists(fullPath))
                {
                    throw new InvalidOperationException($"Tokenset '{set}' does not exist.");
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

    /// <summary>API route to count the CLIP-like tokens in a given text prompt.</summary>
    public static async Task<JObject> CountTokens(string text, string tokenset = "clip")
    {
        (JObject error, CliplikeTokenizer tokenizer) = GetTokenizerForAPI(text, tokenset);
        if (error != null)
        {
            return error;
        }
        int count = tokenizer.Encode(text).Length;
        return new JObject() { ["count"] = count };
    }

    /// <summary>API route to tokenize some prompt text and get thorough detail about it.</summary>
    public static async Task<JObject> TokenizeInDetail(string text, string tokenset = "clip")
    {
        (JObject error, CliplikeTokenizer tokenizer) = GetTokenizerForAPI(text, tokenset);
        if (error != null)
        {
            return error;
        }
        int[] tokens = tokenizer.Encode(text);
        return new JObject()
        {
            ["tokens"] = new JArray(tokens.Select(t => new JObject() { ["id"] = t, ["text"] = tokenizer.Tokens[t] }).ToArray())
        };
    }
}
