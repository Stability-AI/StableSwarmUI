using FreneticUtilities.FreneticExtensions;

namespace StableSwarmUI.Utils;

/// <summary>Helper class to regionalize a prompt.</summary>
public class PromptRegion
{
    public string GlobalPrompt = "";

    public string BackgroundPrompt = "";

    public enum PartType
    {
        Region, Object, Segment, ClearSegment
    }

    public class Part
    {
        public string Prompt;

        public float X, Y, Width, Height;

        public double Strength = 1;

        public double Strength2 = 1;

        public string DataText;

        public PartType Type;

        public int ContextID;
    }

    public List<Part> Parts = [];

    public PromptRegion()
    {
    }

    public PromptRegion(string prompt)
    {
        if (!prompt.Contains("<region:") && !prompt.Contains("<object:") && !prompt.Contains("<segment:") && !prompt.Contains("<clear:"))
        {
            GlobalPrompt = prompt;
            return;
        }
        string[] pieces = prompt.Split('<');
        bool first = true;
        Action<string> addMore = s => GlobalPrompt += s;
        int id = -1;
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
            (string tagBefore, string cidText) = tag.BeforeAndAfterLast("//cid=");
            if (!string.IsNullOrWhiteSpace(cidText) && int.TryParse(cidText, out int cid))
            {
                id = cid;
                tag = tagBefore;
            }
            (string prefix, string regionData) = tag.BeforeAndAfter(':');
            string content = piece[(end + 1)..];
            PartType type;
            if (prefix == "region")
            {
                type = PartType.Region;
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
            }
            else if (prefix == "object")
            {
                type = PartType.Object;
            }
            else if (prefix == "segment")
            {
                type = PartType.Segment;
            }
            else if (prefix == "clear")
            {
                type = PartType.ClearSegment;
            }
            else
            {
                addMore($"<{piece}");
                continue;
            }
            Part p = new()
            {
                Prompt = content,
                Type = type,
                ContextID = id
            };
            string[] coords = regionData.Split(',');
            if (type == PartType.Segment || type == PartType.ClearSegment)
            {
                p.DataText = regionData;
                if (coords.Length > 1 && float.TryParse(coords[^1], out float x))
                {
                    p.Strength = Math.Clamp(x, 0, 1);
                    p.DataText = coords.SkipLast(1).JoinString(",");
                }
                else
                {
                    p.Strength = 0.5;
                }
                if (coords.Length > 2 && float.TryParse(coords[^2], out float y))
                {
                    p.Strength2 = Math.Clamp(y, 0, 1);
                    p.DataText = coords.SkipLast(2).JoinString(",");
                }
                else
                {
                    p.Strength2 = 0.6;
                }
            }
            else
            {
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
                double strength2 = coords.Length > 5 && double.TryParse(coords[5], out double s2) ? s2 : 1.0;
                x = Math.Clamp(x, 0, 1);
                y = Math.Clamp(y, 0, 1);
                p.Strength = Math.Clamp(strength, 0, 1);
                p.Strength2 = Math.Clamp(strength2, 0, 1);
                p.X = x;
                p.Y = y;
                p.Width = Math.Clamp(width, 0, 1 - x);
                p.Height = Math.Clamp(height, 0, 1 - y);
            }
            Parts.Add(p);
            addMore = s => p.Prompt += s;
        }
        foreach (Part part in Parts)
        {
            if (part.Type == PartType.Segment && string.IsNullOrWhiteSpace(part.Prompt))
            {
                part.Prompt = GlobalPrompt;
            }
        }
    }
}
