namespace StableSwarmUI.Utils;

/// <summary>Helper class to regionalize a prompt.</summary>
public class PromptRegion
{
    public string GlobalPrompt = "";

    public string BackgroundPrompt = "";

    public class Part
    {
        public string Prompt;

        public float X, Y, Width, Height;

        public double Strength = 1.0;
    }

    public List<Part> Parts = new();

    public PromptRegion()
    {
    }

    public PromptRegion(string prompt)
    {
        if (!prompt.Contains("<region:"))
        {
            GlobalPrompt = prompt;
            return;
        }
        string[] pieces = prompt.Split("<region:");
        bool isGlobal = true, isBackground = false;
        foreach (string piece in pieces)
        {
            if (isGlobal)
            {
                GlobalPrompt += piece;
                isGlobal = false;
                isBackground = false;
                continue;
            }
            if (isBackground)
            {
                BackgroundPrompt += piece;
                isGlobal = false;
                isBackground = false;
                continue;
            }
            int end = piece.IndexOf('>');
            if (end == -1)
            {
                GlobalPrompt += "<region:" + piece;
                continue;
            }
            string regionData = piece[..end];
            if (regionData == "end")
            {
                GlobalPrompt += piece[(end + 1)..];
                isGlobal = true;
                continue;
            }
            if (regionData == "background")
            {
                BackgroundPrompt += piece[(end + 1)..];
                isBackground = true;
                continue;
            }
            string[] coords = regionData.Split(',');
            if (coords.Length < 4 || coords.Length > 5
                || !float.TryParse(coords[0], out float x)
                || !float.TryParse(coords[1], out float y)
                || !float.TryParse(coords[2], out float width)
                || !float.TryParse(coords[3], out float height))
            {
                GlobalPrompt += "<region:" + piece;
                continue;
            }
            double strength = coords.Length > 4 && double.TryParse(coords[4], out double s) ? s : 1.0;
            string regionPrompt = piece[(end + 1)..];
            Parts.Add(new Part
            {
                Prompt = regionPrompt,
                Strength = strength,
                X = x,
                Y = y,
                Width = width,
                Height = height
            });
        }
    }
}
