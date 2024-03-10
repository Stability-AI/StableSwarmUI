using FreneticUtilities.FreneticToolkit;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace StableSwarmUI.Utils;

/// <summary>This class can interpret a CLIP-like token set and tokenize it properly (compatible/equivalent with OpenCLIP tokenization results).</summary>
public partial class CliplikeTokenizer
{
    /// <summary>The raw array of tokens, wherein a numerical index corresponds to the Token ID.</summary>
    public string[] Tokens;

    /// <summary>A small struct of data about a token.</summary>
    /// <param name="ID">The numerical ID for the token.</param>
    /// <param name="Piece">The text-piece string this token represents.</param>
    public record struct TokenData(int ID, string Piece);

    /// <summary>Optimized dense lookup table optimization trick.</summary>
    public List<TokenData>[] DataMap = new List<TokenData>[128 * 128];

    /// <summary>Cache of already-computed tokenizations (for words, not complete texts).</summary>
    public ConcurrentDictionary<string, int[]> Cache = new();

    /// <summary>Gets the lookup-table optimization index for a given piece of text.</summary>
    public static int GetIndex(string word)
    {
        int c1 = Math.Min((int)word[0], 127);
        int c2 = word.Length > 1 ? Math.Min((int)word[1], 127) : 0;
        return c1 * 128 + c2;
    }

    /// <summary>Causes this instance to immediately load and process the tokenset data at the given filepath. Can be a '.txt' or a '.txt.gz'.</summary>
    public void Load(string fname)
    {
        byte[] data = File.ReadAllBytes(fname);
        if (fname.EndsWith(".gz"))
        {
            using MemoryStream inStream = new(data);
            using GZipStream gZipStream = new(inStream, CompressionMode.Decompress);
            using MemoryStream outStream = new();
            gZipStream.CopyTo(outStream);
            data = outStream.ToArray();
        }
        Tokens = StringConversionHelper.UTF8Encoding.GetString(data).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < DataMap.Length; i++)
        {
            DataMap[i] = [];
        }
        for (int i = 0; i < Tokens.Length; i++)
        {
            string token = Tokens[i];
            DataMap[GetIndex(token)].Add(new TokenData(i, token));
            if (token.Length == 1) // Ensure multi-letter words still read single-letter tokens
            {
                int baseIndex = GetIndex(token);
                for (int x = 1; x < 128; x++)
                {
                    DataMap[baseIndex + x].Add(new TokenData(i, token));
                }
            }
        }
        for (int i = 0; i < DataMap.Length; i++)
        {
            DataMap[i] = [.. DataMap[i].OrderByDescending(t => t.Piece.Length)];
        }
    }

    /// <summary>Creates and returns the token ID set encoding for a given single word.</summary>
    public int[] EncodeWord(string word)
    {
        if (Cache is not null && Cache.TryGetValue(word, out int[] result))
        {
            return result;
        }
        IEnumerable<TokenData> tokens = DataMap[GetIndex(word)];
        int[] best = null;
        foreach (TokenData token in tokens)
        {
            if (token.Piece.Length > word.Length)
            {
                continue;
            }
            if (word == token.Piece)
            {
                return [token.ID];
            }
            if (token.Piece == word[0..token.Piece.Length])
            {
                int[] subseq = EncodeWord(word[token.Piece.Length..]);
                if (best is null || subseq.Length + 1 < best.Length)
                {
                    best = new int[subseq.Length + 1];
                    best[0] = token.ID;
                    Array.Copy(subseq, 0, best, 1, subseq.Length);
                }
            }
        }
        if (best is null)
        {
            Logs.Debug($"[CliplikeTokenizer] Error: Cannot encode word '{word}', will emit empty");
            return [];
        }
        if (Cache is not null)
        {
            Cache[word] = best;
        }
        return best;
    }

    /// <summary>Compiler-generated-regex for <see cref="Splitter"/>.</summary>
    [GeneratedRegex("'s|'t|'re|'ve|'m|'ll|'d|[\\p{L}]+|[\\p{N}]|[^\\s\\p{L}\\p{N}]+", RegexOptions.Compiled)]
    private static partial Regex GenSplitter();
    /// <summary>Special regex (matches OpenCLIP source) for where '/w' splitters should apply.</summary>
    public static Regex Splitter = GenSplitter();

    /// <summary>Holds the data for an encoded token.</summary>
    /// <param name="ID">The token ID.</param>
    /// <param name="Weight">The token weight.</param>
    public record struct Token(int ID, float Weight);

    /// <summary>Encodes a given chunk of text, and returns the raw encoded token set.</summary>
    public Token[] Encode(string text, float weight = 1, bool fixParens = false)
    {
        if (fixParens)
        {
            text = text.Replace("\\(", "(").Replace("\\)", ")");
        }
        List<Token> output = [];
        foreach (string word in Splitter.Matches(text.ToLowerInvariant()).Select(m => m.Value))
        {
            if (!string.IsNullOrWhiteSpace(word))
            {
                foreach (int token in EncodeWord(word + "</w>"))
                {
                    output.Add(new(token, weight));
                }
            }
        }
        return [.. output];
    }

    /// <summary>Encodes a given chunk of text (with parenthetical weight parsing), and returns the raw encoded token set.
    /// <para>Uses ComfyUI-style weighting.</para></summary>
    public Token[] EncodeWithWeighting(string text, float weight = 1)
    {
        int depth = 0;
        char[] data = [.. text];
        int start = 0;
        int parenStart = 0;
        List<Token> output = [];
        for (int i = 0; i < data.Length; i++)
        {
            char c = data[i];
            if (c == '(' && (i == 0 || data[i - 1] != '\\'))
            {
                depth++;
                if (depth == 1)
                {
                    parenStart = i;
                }
            }
            else if (c == ')' && depth > 0 && (i == 0 || data[i - 1] != '\\'))
            {
                depth--;
                if (depth == 0)
                {
                    string prefix = text[start..parenStart];
                    if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        output.AddRange(Encode(prefix, weight, true));
                    }
                    start = parenStart;
                    string paren = text[(start + 1)..i];
                    if (!string.IsNullOrWhiteSpace(paren))
                    {
                        int lastColon = paren.LastIndexOf(':');
                        if (lastColon != -1 && float.TryParse(paren[(lastColon + 1)..], out float subWeight))
                        {
                            paren = paren[..lastColon];
                        }
                        else
                        {
                            subWeight = weight * 1.1f;
                        }
                        output.AddRange(EncodeWithWeighting(paren, subWeight));
                        start = i + 1;
                    }
                }
            }
        }
        if (start < text.Length)
        {
            output.AddRange(Encode(text[start..], weight, true));
        }
        return [.. output];
    }
}
