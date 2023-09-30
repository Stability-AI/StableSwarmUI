using FreneticUtilities.FreneticExtensions;

namespace StableSwarmUI.Utils;

/// <summary>Helper class to regionalize a prompt.</summary>
public class PromptRegion
{
    public string GlobalPrompt = "";

    public string BackgroundPrompt = "";

    public enum PartType
    {
        Region, Object
    }

    public class Part
    {
        public string Prompt;

        public float X, Y, Width, Height;

        public double Strength;

        public double Strength2;

        public PartType Type;
    }

    public List<Part> Parts = new();

    public PromptRegion()
    {
    }

    public PromptRegion(string prompt)
    {
        if (!prompt.Contains("<region:") && !prompt.Contains("<object:"))
        {
            GlobalPrompt = prompt;
            return;
        }
        string[] pieces = prompt.Split('<');
        bool first = true;
        Action<string> addMore = s => GlobalPrompt += s;
        foreach (string piece in pieces)
        {
            if (first)
            {
                first = false;
                addMore(piece);
                continue;
            }
            int end = piece.IndexOf('>');
            if (end == -1)
            {
                addMore($"<{piece}");
                continue;
            }
            string tag = piece[..end];
            (string prefix, string regionData) = tag.BeforeAndAfter(':');
            string content = piece[(end + 1)..];
            PartType type;
            if (prefix == "region")
            {
                type = PartType.Region;
            }
            else if (prefix == "object")
            {
                type = PartType.Object;
            }
            else
            {
                addMore($"<{piece}");
                continue;
            }
            if (regionData == "end")
            {
                GlobalPrompt += content;
                addMore = s => GlobalPrompt += s;
                continue;
            }
            if (regionData == "background")
            {
                BackgroundPrompt += content;
                addMore = s => BackgroundPrompt += s;
                continue;
            }
            string[] coords = regionData.Split(',');
            if (coords.Length < 4 || coords.Length > 6
                || !float.TryParse(coords[0], out float x)
                || !float.TryParse(coords[1], out float y)
                || !float.TryParse(coords[2], out float width)
                || !float.TryParse(coords[3], out float height))
            {
                addMore($"<{piece}");
                continue;
            }
            double strength = coords.Length > 4 && double.TryParse(coords[4], out double s) ? s : 1.0;
            double strength2 = coords.Length > 5 && double.TryParse(coords[5], out double s2) ? s2 : 0.9;
            x = Math.Clamp(x, 0, 1);
            y = Math.Clamp(y, 0, 1);
            Part p = new()
            {
                Prompt = content,
                Strength = Math.Clamp(strength, 0, 1),
                Strength2 = Math.Clamp(strength2, 0, 1),
                X = x,
                Y = y,
                Width = Math.Clamp(width, 0, 1 - x),
                Height = Math.Clamp(height, 0, 1 - y),
                Type = type
            };
            Parts.Add(p);
            addMore = s => p.Prompt += s;
        }
    }
}
