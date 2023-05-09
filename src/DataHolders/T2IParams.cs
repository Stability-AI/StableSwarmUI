using static StableUI.DataHolders.IDataHolder;

namespace StableUI.DataHolders;

/// <summary>Holds the parameters of a text-to-image call.</summary>
public class T2IParams : IDataHolder
{
    [NetData(Name = "prompt")]
    public string Prompt = "";

    [NetData(Name = "negative_prompt")]
    public string NegativePrompt = "";

    [NetData(Name = "cfg_scale")]
    public float CFGScale = 7;

    [NetData(Name = "seed")]
    public int Seed = -1;

    [NetData(Name = "width")]
    public int Width = 512;

    [NetData(Name = "height")]
    public int Height = 512;

    [NetData(Name = "steps")]
    public int Steps = 2;

    public T2IParams Clone()
    {
        return MemberwiseClone() as T2IParams;
    }
}
