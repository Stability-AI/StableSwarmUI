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

    [NetData(Name = "negativeprompt")]
    public string NegativePrompt = "";

    [NetData(Name = "cfgscale")]
    public float CFGScale = 7;

    [NetData(Name = "seed")]
    public long Seed = -1;

    [NetData(Name = "width")]
    public int Width = 512;

    [NetData(Name = "height")]
    public int Height = 512;

    [NetData(Name = "steps")]
    public int Steps = 20;

    [NetData(Name = "variationseed")]
    public long VarSeed = -1;

    [NetData(Name = "variationseedstrength")]
    public float VarSeedStrength = 0;

    [NetData(Name = "internalbackendtype")]
    public string BackendType = "any";

    [NetData(Name = "imageinitcreativity")]
    public float ImageInitStrength = 0.6f;

    /// <summary>Arbitrary ID to uniquely identify a batch of images.</summary>
    public int BatchID;

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
        res.RequiredFlags = new(RequiredFlags);
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

    public string GenMetadata(Dictionary<string, object> extraParams)
    {
        JObject data = new()
        {
            ["prompt"] = Prompt,
            ["negativeprompt"] = NegativePrompt,
            ["cfgscale"] = CFGScale,
            ["seed"] = Seed,
            ["width"] = Width,
            ["height"] = Height,
            ["steps"] = Steps,
            ["variationseed"] = VarSeed,
            ["variationseedstrength"] = VarSeedStrength,
            ["imageinitcreativity"] = ImageInitStrength,
            ["model"] = Model.Name
        };
        if (BackendType != "any")
        {
            data["internalbackendtype"] = BackendType;
        }
        foreach ((string key, object val) in OtherParams)
        {
            if (val is not null)
            {
                data[key] = JToken.FromObject(val);
            }
        }
        if (extraParams is not null)
        {
            foreach ((string key, object val) in extraParams)
            {
                if (val is not null)
                {
                    data[key] = JToken.FromObject(val);
                }
            }
        }
        return new JObject() { ["stableui_image_params"] = data }.ToString();
    }
}
