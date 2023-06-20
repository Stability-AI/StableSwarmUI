using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using StableUI.Accounts;
using StableUI.Backends;
using StableUI.Text2Image;
using StableUI.Utils;
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
    public long Seed = -1;

    [NetData(Name = "width")]
    public int Width = 512;

    [NetData(Name = "height")]
    public int Height = 512;

    [NetData(Name = "steps")]
    public int Steps = 20;

    [NetData(Name = "var_seed")]
    public long VarSeed = -1;

    [NetData(Name = "var_seed_strength")]
    public float VarSeedStrength = 0;

    [NetData(Name = "backend_type")]
    public string BackendType = "any";

    [NetData(Name = "image_init_strength")]
    public float ImageInitStrength = 0.6f;

    /// <summary>What model the user wants this image generated with.</summary>
    public T2IModel Model;

    /// <summary>Optional external data, from eg an extension that needs its own data tracking.</summary>
    public IDataHolder ExternalData;

    /// <summary>General-purpose holder of other parameters to pass along.</summary>
    public Dictionary<string, object> OtherParams = new();

    /// <summary>The session this request came from, if known.</summary>
    public Session SourceSession;

    /// <summary>What feature flags, if any, are required by this request.</summary>
    public HashSet<string> RequiredFlags = new();

    /// <summary>Optional initialization image for img2img generations.</summary>
    public Image InitImage;

    /// <summary>Interrupt token from the session.</summary>
    public CancellationToken InterruptToken;

    public T2IParams(Session session)
    {
        SourceSession = session;
        InterruptToken = session.SessInterrupt.Token;
    }

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

    public JObject ToJson()
    {
        return new JObject()
        {
            ["prompt"] = Prompt,
            ["negative_prompt"] = NegativePrompt,
            ["cfg_scale"] = CFGScale,
            ["seed"] = Seed,
            ["width"] = Width,
            ["height"] = Height,
            ["steps"] = Steps,
            ["var_seed"] = VarSeed,
            ["var_seed_strength"] = VarSeedStrength,
            ["image_init_strength"] = ImageInitStrength,
            ["model"] = Model.Name,
            ["other_params"] = JObject.FromObject(OtherParams)
        };
    }

    public string GenMetadata()
    {
        return new JObject() { ["stableui_image_params"] = ToJson() }.ToString();
    }
}
