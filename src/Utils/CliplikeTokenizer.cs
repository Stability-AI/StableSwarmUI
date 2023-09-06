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
            DataMap[i] = new();
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
            DataMap[i] = DataMap[i].OrderByDescending(t => t.Piece.Length).ToList();
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
                return new int[1] { token.ID };
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
            return Array.Empty<int>();
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

    /// <summary>Encodes a given chunk of text, and returns the raw encoded token set.</summary>
    public int[] Encode(string text)
    {
        List<int> output = new();
        foreach (string word in Splitter.Matches(text.ToLowerInvariant()).Select(m => m.Value))
        {
            if (!string.IsNullOrWhiteSpace(word))
            {
                output.AddRange(EncodeWord(word + "</w>"));
            }
        }
        return output.ToArray();
    }
}
