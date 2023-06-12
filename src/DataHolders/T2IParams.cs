using FreneticUtilities.FreneticExtensions;
using StableUI.Accounts;
using StableUI.Backends;
using StableUI.Text2Image;
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
    public int Steps = 20;

    [NetData(Name = "var_seed")]
    public int VarSeed = -1;

    [NetData(Name = "var_seed_strength")]
    public float VarSeedStrength = 0;

    [NetData(Name = "backend_type")]
    public string BackendType = "any";

    public T2IModel Model;

    public IDataHolder ExternalData;

    public Dictionary<string, object> OtherParams = new();

    /// <summary>The session this request came from, if known.</summary>
    public Session SourceSession;

    /// <summary>What feature flags, if any, are required by this request.</summary>
    public HashSet<string> RequiredFlags = new();

    public T2IParams Clone()
    {
        T2IParams res = MemberwiseClone() as T2IParams;
        if (res.ExternalData is not null)
        {
            res.ExternalData = res.ExternalData.Clone();
        }
        res.OtherParams = new(OtherParams);
        return res;
    }

    IDataHolder IDataHolder.Clone() => Clone();

    public bool BackendMatcher(BackendHandler.T2IBackendData backend)
    {
        if (BackendType != "any" && BackendType.ToLowerFast() != backend.Backend.HandlerTypeData.ID.ToLowerFast())
        {
            return false;
        }
        foreach (string flag in RequiredFlags)
        {
            if (!backend.Backend.DoesProvideFeature(flag))
            {
                return false;
            }
        }
        return true;
    }
}
