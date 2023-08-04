using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;

namespace StableSwarmUI.Text2Image;

/// <summary>Represents user-input for a Text2Image request.</summary>
public class T2IParamInput
{
    /// <summary>The raw values in this input. Do not use this directly, instead prefer:
    /// <see cref="Get{T}(T2IRegisteredParam{T})"/>, <see cref="TryGet{T}(T2IRegisteredParam{T}, out T)"/>,
    /// <see cref="Set{T}(T2IRegisteredParam{T}, string)"/>.</summary>
    public Dictionary<string, object> ValuesInput = new();

    /// <summary>A set of feature flags required for this input.</summary>
    public HashSet<string> RequiredFlags = new();

    /// <summary>The session this input came from.</summary>
    public Session SourceSession;

    /// <summary>Interrupt token from the session.</summary>
    public CancellationToken InterruptToken;

    /// <summary>Construct a new parameter input handler for a session.</summary>
    public T2IParamInput(Session session)
    {
        SourceSession = session;
        InterruptToken = session.SessInterrupt.Token;
    }

    /// <summary>Returns a perfect duplicate of this parameter input, with new reference addresses.</summary>
    public T2IParamInput Clone()
    {
        T2IParamInput toret = MemberwiseClone() as T2IParamInput;
        toret.ValuesInput = new Dictionary<string, object>(ValuesInput);
        toret.RequiredFlags = new HashSet<string>(RequiredFlags);
        return toret;
    }

    /// <summary>Generates a metadata JSON object for this input and the given set of extra parameters.</summary>
    public JObject GenMetadataObject(Dictionary<string, object> extraParams)
    {
        JObject output = new();
        foreach ((string key, object val) in ValuesInput.Union(extraParams))
        {
            if (T2IParamTypes.TryGetType(key, out T2IParamType type, this) && type.HideFromMetadata)
            {
                continue;
            }
            if (val is Image)
            {
                continue;
            }
            if (val is T2IModel model)
            {
                output[key] = model.Name;
            }
            else
            {
                output[key] = JToken.FromObject(val);
            }
        }
        return output;
    }

    /// <summary>Generates a metadata JSON object for this input and creates a proper string form of it, fit for inclusion in an image.</summary>
    public string GenRawMetadata(Dictionary<string, object> extraParams)
    {
        JObject obj = GenMetadataObject(extraParams);
        return new JObject() { ["sui_image_params"] = obj }.ToString(Newtonsoft.Json.Formatting.Indented);
    }

    /// <summary>Gets the raw value of the parameter, if it is present, or null if not.</summary>
    public object GetRaw(T2IParamType param)
    {
        return ValuesInput.GetValueOrDefault(param.ID);
    }

    /// <summary>Gets the value of the parameter, if it is present, or default if not.</summary>
    public T Get<T>(T2IRegisteredParam<T> param)
    {
        if (ValuesInput.TryGetValue(param.Type.ID, out object val))
        {
            if (val is long lVal && typeof(T) == typeof(int))
            {
                val = (int)lVal;
            }
            if (val is double dVal && typeof(T) == typeof(float))
            {
                val = (float)dVal;
            }
            return (T)val;
        }
        return default;
    }

    /// <summary>Gets the value of the parameter as a string, if it is present, or null if not.</summary>
    public string GetString<T>(T2IRegisteredParam<T> param)
    {
        if (ValuesInput.TryGetValue(param.Type.ID, out object val))
        {
            return $"{(T)val}";
        }
        return null;
    }

    /// <summary>Tries to get the value of the parameter. If it is present, returns true and outputs the value. If it is not present, returns false.</summary>
    public bool TryGet<T>(T2IRegisteredParam<T> param, out T val)
    {
        if (ValuesInput.TryGetValue(param.Type.ID, out object valObj))
        {
            val = (T)valObj;
            return true;
        }
        val = default;
        return false;
    }

    /// <summary>Tries to get the value of the parameter. If it is present, returns true and outputs the value. If it is not present, returns false.</summary>
    public bool TryGetRaw(T2IParamType param, out object val)
    {
        if (ValuesInput.TryGetValue(param.ID, out object valObj))
        {
            val = valObj;
            return true;
        }
        val = default;
        return false;
    }

    /// <summary>Sets the value of an input parameter to a given plaintext input. Will run the 'Clean' call if needed.</summary>
    public void Set(T2IParamType param, string val)
    {
        if (param.Clean is not null)
        {
            val = param.Clean(ValuesInput.TryGetValue(param.ID, out object valObj) ? valObj.ToString() : null, val);
        }
        T2IModel getModel(string name)
        {
            T2IModelHandler handler = Program.T2IModelSets[param.Subtype ?? "Stable-Diffusion"];
            string best = T2IParamTypes.GetBestInList(name.Replace('\\', '/'), handler.Models.Keys.ToList());
            return handler.Models[best];
        }
        object obj = param.Type switch
        {
            T2IParamDataType.INTEGER => long.Parse(val),
            T2IParamDataType.DECIMAL => double.Parse(val),
            T2IParamDataType.BOOLEAN => bool.Parse(val),
            T2IParamDataType.TEXT or T2IParamDataType.DROPDOWN => val,
            T2IParamDataType.IMAGE => new Image(val),
            T2IParamDataType.MODEL => getModel(val),
            T2IParamDataType.LIST => val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            _ => throw new NotImplementedException()
        };
        ValuesInput[param.ID] = obj;
    }

    /// <summary>Sets the direct raw value of a given parameter, without processing.</summary>
    public void Set<T>(T2IRegisteredParam<T> param, T val)
    {
        if (param.Type.Clean is not null)
        {
            Set(param.Type, val.ToString());
            return;
        }
        ValuesInput[param.Type.ID] = val;
    }

    /// <summary>Makes sure the input has valid seed inputs.</summary>
    public void NormalizeSeeds()
    {
        if (!TryGet(T2IParamTypes.Seed, out long seed) || seed == -1)
        {
            Set(T2IParamTypes.Seed, Random.Shared.Next());
        }
        if (TryGet(T2IParamTypes.VariationSeed, out long vseed) && vseed == -1)
        {
            Set(T2IParamTypes.VariationSeed, Random.Shared.Next());
        }
    }

    public override string ToString()
    {
        return $"T2IParamInput({string.Join(", ", ValuesInput.Select(x => $"{x.Key}: {x.Value}"))})";
    }
}
