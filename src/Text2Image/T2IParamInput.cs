using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;
using System;
using System.IO;

namespace StableSwarmUI.Text2Image;

/// <summary>Represents user-input for a Text2Image request.</summary>
public class T2IParamInput
{
    public class PromptTagContext
    {
        public Random Random;

        public T2IParamInput Input;

        public string Param;
    }

    /// <summary>Mapping of prompt tag prefixes, to allow for registration of custom prompt tags.</summary>
    public static Dictionary<string, Func<string, PromptTagContext, string>> PromptTagProcessors = new();

    static T2IParamInput()
    {
        PromptTagProcessors["random"] = (data, context) =>
        {
            string separator = data.Contains("||") ? "||" : (data.Contains('|') ? "|" : ",");
            string[] vals = data.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (vals.Length == 0)
            {
                return null;
            }
            return vals[context.Random.Next(vals.Length)];
        };
        PromptTagProcessors["preset"] = (data, context) =>
        {
            T2IPreset preset = context.Input.SourceSession.User.GetPreset(data);
            if (preset is null)
            {
                return null;
            }
            preset.ApplyTo(context.Input);
            if (preset.ParamMap.TryGetValue(context.Param, out string prompt))
            {
                return "\0" + prompt;
            }
            return "";
        };
        // TODO: Wildcards
    }

    /// <summary>The raw values in this input. Do not use this directly, instead prefer:
    /// <see cref="Get{T}(T2IRegisteredParam{T})"/>, <see cref="TryGet{T}(T2IRegisteredParam{T}, out T)"/>,
    /// <see cref="Set{T}(T2IRegisteredParam{T}, string)"/>.</summary>
    public Dictionary<string, object> ValuesInput = new();

    /// <summary>Extra data to store in metadata.</summary>
    public Dictionary<string, object> ExtraMeta = new();

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
        ExtraMeta["swarm_version"] = Utilities.Version;
        ExtraMeta["date"] = DateTime.Now.ToString("yyyy-MM-dd");
    }

    /// <summary>Gets the desired image width, automatically using alt-res parameter if needed.</summary>
    public int GetImageHeight()
    {
        if (TryGet(T2IParamTypes.AltResolutionHeightMult, out double val)
            && TryGet(T2IParamTypes.Width, out int width))
        {
            return (int)(val * width);
        }
        return Get(T2IParamTypes.Height, 512);
    }

    /// <summary>Returns a perfect duplicate of this parameter input, with new reference addresses.</summary>
    public T2IParamInput Clone()
    {
        T2IParamInput toret = MemberwiseClone() as T2IParamInput;
        toret.ValuesInput = new Dictionary<string, object>(ValuesInput);
        toret.ExtraMeta = new Dictionary<string, object>(ExtraMeta);
        toret.RequiredFlags = new HashSet<string>(RequiredFlags);
        return toret;
    }

    /// <summary>Generates a JSON object for this input that can be fed straight back into the Swarm API.</summary>
    public JObject ToJSON()
    {
        JObject result = new();
        foreach ((string key, object val) in ValuesInput)
        {
            if (val is Image img)
            {
                result[key] = img.AsBase64;
            }
            else if (val is List<Image> imgList)
            {
                result[key] = imgList.Select(img => img.AsBase64).JoinString("|");
            }
            else if (val is T2IModel model)
            {
                result[key] = model.Name;
            }
            else
            {
                result[key] = JToken.FromObject(val);
            }
        }
        return result;
    }

    /// <summary>Generates a metadata JSON object for this input and the given set of extra parameters.</summary>
    public JObject GenMetadataObject()
    {
        JObject output = new();
        foreach ((string key, object val) in ValuesInput.Union(ExtraMeta))
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
    public string GenRawMetadata()
    {
        JObject obj = GenMetadataObject();
        return new JObject() { ["sui_image_params"] = obj }.ToString(Newtonsoft.Json.Formatting.Indented);
    }

    /// <summary>Special utility to process prompt inputs before the request is executed (to parse wildcards, embeddings, etc).</summary>
    public void PreparsePromptLikes(Func<string, string> embedFormatter)
    {
        ValuesInput["prompt"] = ProcessPromptLike(T2IParamTypes.Prompt, embedFormatter);
        ValuesInput["negativeprompt"] = ProcessPromptLike(T2IParamTypes.NegativePrompt, embedFormatter);
    }

    /// <summary>Special utility to process prompt inputs before the request is executed (to parse wildcards, embeddings, etc).</summary>
    public string ProcessPromptLike(T2IRegisteredParam<string> param, Func<string, string> embedFormatter)
    {
        string val = Get(param);
        if (val is null)
        {
            return null;
        }
        Random rand = new((int)Get(T2IParamTypes.Seed) + param.Type.Name.Length);
        string lowRef = val.ToLowerFast();
        string[] embeds = lowRef.Contains("<embed") ? Program.T2IModelSets["Embedding"].ListModelsFor(SourceSession).Select(m => m.Name).ToArray() : null;
        string[] loras = lowRef.Contains("<lora:") ? Program.T2IModelSets["LoRA"].ListModelsFor(SourceSession).Select(m => m.Name.ToLowerFast()).ToArray() : null;
        PromptTagContext context = new() { Input = this, Random = rand, Param = param.Type.ID };
        string addBefore = "", addAfter = "";
        string fixedVal = StringConversionHelper.QuickSimpleTagFiller(val, "<", ">", tag =>
        {
            (string prefix, string data) = tag.BeforeAndAfter(':');
            if (string.IsNullOrWhiteSpace(data))
            {
                return $"<{tag}>";
            }
            switch (prefix.ToLowerFast())
            {
                case "embed":
                case "embedding":
                    {
                        if (embeds is not null)
                        {
                            string want = data.ToLowerFast().Replace('\\', '/');
                            string matched = embeds.FirstOrDefault(e => e.ToLowerFast().StartsWith(want)) ?? embeds.FirstOrDefault(e => e.ToLowerFast().Contains(want));
                            if (matched is not null)
                            {
                                data = matched;
                            }
                        }
                        return embedFormatter(data.Replace('/', Path.DirectorySeparatorChar));
                    }
                case "lora":
                    {
                        string lora = data.ToLowerFast().Replace('\\', '/');
                        int colonIndex = lora.IndexOf(':');
                        double strength = 1;
                        if (colonIndex != -1 && double.TryParse(lora[(colonIndex + 1)..], out strength))
                        {
                            lora = lora[..colonIndex];
                        }
                        string matched = loras.FirstOrDefault(e => e.ToLowerFast().StartsWith(lora)) ?? loras.FirstOrDefault(e => e.ToLowerFast().Contains(lora));
                        if (matched is not null)
                        {
                            List<string> loraList = Get(T2IParamTypes.Loras);
                            List<string> weights = Get(T2IParamTypes.LoraWeights);
                            if (loraList is null)
                            {
                                loraList = new();
                                weights = new();
                            }
                            loraList.Add(matched);
                            weights.Add(strength.ToString());
                            Set(T2IParamTypes.Loras, loraList);
                            Set(T2IParamTypes.LoraWeights, weights);
                            return "";
                        }
                        else
                        {
                            return $"<{tag}>";
                        }
                    }
                default:
                    if (PromptTagProcessors.TryGetValue(prefix, out Func<string, PromptTagContext, string> proc))
                    {
                        string result = proc(data, context);
                        if (result is not null)
                        {
                            if (result.StartsWithNull()) // Special case for preset tag modifying the current value
                            {
                                result = result[1..];
                                if (result.Contains("{value}"))
                                {
                                    addBefore += result.Before("{value}");
                                }
                                addAfter += result.After("{value}");
                                return "";
                            }
                            return result;
                        }
                    }
                    return $"<{tag}>";
            }
        });
        fixedVal = addBefore + fixedVal + addAfter;
        if (fixedVal != val)
        {
            ExtraMeta[$"original_{param.Type.ID}"] = val;
        }
        return fixedVal;
    }

    /// <summary>Gets the raw value of the parameter, if it is present, or null if not.</summary>
    public object GetRaw(T2IParamType param)
    {
        return ValuesInput.GetValueOrDefault(param.ID);
    }

    /// <summary>Gets the value of the parameter, if it is present, or default if not.</summary>
    public T Get<T>(T2IRegisteredParam<T> param) => Get(param, default);

    /// <summary>Gets the value of the parameter, if it is present, or default if not.</summary>
    public T Get<T>(T2IRegisteredParam<T> param, T defVal)
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
        return defVal;
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
            T2IParamDataType.INTEGER => param.SharpType == typeof(long) ? long.Parse(val) : int.Parse(val),
            T2IParamDataType.DECIMAL => param.SharpType == typeof(double) ? double.Parse(val) : float.Parse(val),
            T2IParamDataType.BOOLEAN => bool.Parse(val),
            T2IParamDataType.TEXT or T2IParamDataType.DROPDOWN => val,
            T2IParamDataType.IMAGE => new Image(val),
            T2IParamDataType.IMAGE_LIST => val.Split('|').Select(v => new Image(v)).ToList(),
            T2IParamDataType.MODEL => getModel(val),
            T2IParamDataType.LIST => val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            _ => throw new NotImplementedException()
        };
        if (param.SharpType == typeof(int))
        {
            obj = (int)(long)obj; // WTF. Yes this is needed. No I can't explain why. Ternaries are broken maybe?
        }
        if (param.SharpType == typeof(float))
        {
            obj = (float)(double)obj;
        }
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
