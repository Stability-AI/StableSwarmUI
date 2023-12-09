using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;
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

        public string[] Embeds, Loras;

        public int Depth = 0;

        public string Parse(string text)
        {
            if (Depth > 1000)
            {
                Logs.Error("Recursive prompt tags - infinite loop, cannot return valid result.");
                return text;
            }
            Depth++;
            string result = Input.ProcessPromptLike(text, this);
            Depth--;
            return result;
        }
    }

    /// <summary>Mapping of prompt tag prefixes, to allow for registration of custom prompt tags.</summary>
    public static Dictionary<string, Func<string, PromptTagContext, string>> PromptTagProcessors = new();

    /// <summary>Mapping of prompt tag prefixes, to allow for registration of custom prompt tags - specifically post-processing like lora (which remove from prompt and get read elsewhere).</summary>
    public static Dictionary<string, Func<string, PromptTagContext, string>> PromptTagPostProcessors = new();

    /// <summary>Interprets a random number range input by a user, if the input is a number range.</summary>
    public static bool TryInterpretNumberRange(string inputVal, out string number)
    {
        (string preDash, string postDash) = inputVal.BeforeAndAfter('-');
        if (long.TryParse(preDash.Trim(), out long int1) && long.TryParse(postDash.Trim(), out long int2))
        {
            number = $"{Random.Shared.NextInt64(int1, int2 + 1)}";
            return true;
        }
        if (double.TryParse(preDash.Trim(), out double num1) && double.TryParse(postDash.Trim(), out double num2))
        {
            number = $"{Random.Shared.NextDouble() * (num2 - num1) + num1}";
            return true;
        }
        number = null;
        return false;
    }

    /// <summary>Interprets a number input by a user, or returns null if unable to.</summary>
    public static double? InterpretNumber(string inputVal)
    {
        if (TryInterpretNumberRange(inputVal, out string number))
        {
            inputVal = number;
        }
        if (double.TryParse(inputVal.Trim(), out double num))
        {
            return num;
        }
        return null;
    }

    static T2IParamInput()
    {
        PromptTagProcessors["random"] = (data, context) =>
        {
            string separator = data.Contains("||") ? "||" : (data.Contains('|') ? "|" : ",");
            string[] vals = data.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (vals.Length == 0)
            {
                Logs.Warning($"Random input '{data}' is empty and will be ignored.");
                return null;
            }
            string choice = vals[context.Random.Next(vals.Length)];
            if (TryInterpretNumberRange(choice, out string number))
            {
                return number;
            }
            return context.Parse(choice);
        };
        PromptTagProcessors["wildcard"] = (data, context) =>
        {
            string card = T2IParamTypes.GetBestInList(data, WildcardsHelper.ListFiles);
            if (card is null)
            {
                Logs.Warning($"Wildcard input '{data}' does not match any wildcard file and will be ignored.");
                return null;
            }
            List<string> usedWildcards = context.Input.ExtraMeta.GetOrCreate("used_wildcards", () => new List<string>()) as List<string>;
            usedWildcards.Add(card);
            string choice = WildcardsHelper.PickRandom(card, context.Random);
            return context.Parse(choice);
        };
        PromptTagProcessors["repeat"] = (data, context) =>
        {
            (string count, string value) = data.BeforeAndAfter(',');
            double? countVal = InterpretNumber(count);
            if (!countVal.HasValue)
            {
                Logs.Warning($"Repeat input '{data}' has invalid count (not a number) and will be ignored.");
                return null;
            }
            string result = "";
            for (int i = 0; i < countVal.Value; i++)
            {
                result += context.Parse(value).Trim() + " ";
            }
            return result.Trim();
        };
        PromptTagProcessors["preset"] = (data, context) =>
        {
            string name = context.Parse(data);
            T2IPreset preset = context.Input.SourceSession.User.GetPreset(name);
            if (preset is null)
            {
                Logs.Warning($"Preset '{name}' does not exist and will be ignored.");
                return null;
            }
            preset.ApplyTo(context.Input);
            if (preset.ParamMap.TryGetValue(context.Param, out string prompt))
            {
                return "\0preset:" + prompt;
            }
            return "";
        };
        PromptTagProcessors["embed"] = (data, context) =>
        {
            data = context.Parse(data);
            if (context.Embeds is null)
            {
                Logs.Warning($"Embedding '{data}' ignored because the engine has not loaded the embeddings list.");
                return "";
            }
            string want = data.ToLowerFast().Replace('\\', '/');
            string matched = T2IParamTypes.GetBestInList(want, context.Embeds);
            if (matched is null)
            {
                Logs.Warning($"Embedding '{want}' does not exist and will be ignored.");
                return "";
            }
            List<string> usedEmbeds = context.Input.ExtraMeta.GetOrCreate("used_embeddings", () => new List<string>()) as List<string>;
            usedEmbeds.Add(matched);
            return "\0swarmembed:" + matched + "\0end";
        };
        PromptTagProcessors["embedding"] = PromptTagProcessors["embed"];
        PromptTagPostProcessors["lora"] = (data, context) =>
        {
            data = context.Parse(data);
            string lora = data.ToLowerFast().Replace('\\', '/');
            int colonIndex = lora.IndexOf(':');
            double strength = 1;
            if (colonIndex != -1 && double.TryParse(lora[(colonIndex + 1)..], out strength))
            {
                lora = lora[..colonIndex];
            }
            if (context.Loras is null)
            {
                Logs.Warning($"Lora '{data}' ignored because the engine has not loaded the lora list.");
                return "";
            }
            string matched = T2IParamTypes.GetBestInList(lora, context.Loras);
            if (matched is not null)
            {
                List<string> loraList = context.Input.Get(T2IParamTypes.Loras);
                List<string> weights = context.Input.Get(T2IParamTypes.LoraWeights);
                if (loraList is null)
                {
                    loraList = new();
                    weights = new();
                }
                loraList.Add(matched);
                weights.Add(strength.ToString());
                context.Input.Set(T2IParamTypes.Loras, loraList);
                context.Input.Set(T2IParamTypes.LoraWeights, weights);
                return "";
            }
            Logs.Warning($"Lora '{lora}' does not exist and will be ignored.");
            return null;
        };
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
            else if (val is List<string> strList)
            {
                result[key] = strList.JoinString(",");
            }
            else if (val is List<T2IModel> modelList)
            {
                result[key] = modelList.Select(m => m.Name).JoinString(",");
            }
            else if (val is T2IModel model)
            {
                result[key] = model.Name;
            }
            else if (val is string str)
            {
                result[key] = FillEmbedsInString(str, e => $"<embed:{e}>");
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
        foreach ((string key, object origVal) in ValuesInput.Union(ExtraMeta))
        {
            object val = origVal;
            if (val is null)
            {
                Logs.Warning($"Null parameter {key} in T2I parameters?");
                continue;
            }
            if (val is Image)
            {
                continue;
            }
            if (val is string str)
            {
                val = FillEmbedsInString(str, e => $"<embed:{e}>");
            }
            if (T2IParamTypes.TryGetType(key, out T2IParamType type, this))
            {
                if (type.HideFromMetadata)
                {
                    continue;
                }
                if (type.MetadataFormat is not null)
                {
                    val = type.MetadataFormat($"{val}");
                }
            }
            if (val is T2IModel model)
            {
                val = model.Name;
            }
            output[key] = JToken.FromObject(val);
        }
        if (output.TryGetValue("original_prompt", out JToken origPrompt) && output.TryGetValue("prompt", out JToken prompt) && origPrompt == prompt)
        {
            output.Remove("original_prompt");
        }
        if (output.TryGetValue("original_negativeprompt", out JToken origNegPrompt) && output.TryGetValue("negativeprompt", out JToken negPrompt) && origNegPrompt == negPrompt)
        {
            output.Remove("original_negativeprompt");
        }
        return output;
    }

    /// <summary>Aggressively safe JSON Serializer Settings for metadata encoding.</summary>
    public static JsonSerializerSettings SafeSerializer = new() { Formatting = Formatting.Indented, StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };

    /// <summary>Generates a metadata JSON object for this input and creates a proper string form of it, fit for inclusion in an image.</summary>
    public string GenRawMetadata()
    {
        JObject obj = GenMetadataObject();
        return JsonConvert.SerializeObject(new JObject() { ["sui_image_params"] = obj }, SafeSerializer).Replace("\r\n", "\n");
    }

    /// <summary>Special utility to process prompt inputs before the request is executed (to parse wildcards, embeddings, etc).</summary>
    public void PreparsePromptLikes()
    {
        ValuesInput["prompt"] = ProcessPromptLike(T2IParamTypes.Prompt);
        ValuesInput["negativeprompt"] = ProcessPromptLike(T2IParamTypes.NegativePrompt);
    }

    /// <summary>Formats embeddings in a prompt string and returns the cleaned string.</summary>
    public static string FillEmbedsInString(string str, Func<string, string> format)
    {
        return StringConversionHelper.QuickSimpleTagFiller(str, "\0swarmembed:", "\0end", format, false);
    }

    /// <summary>Format embedding text in prompts.</summary>
    public void ProcessPromptEmbeds(Func<string, string> formatEmbed, Func<string, string> generalPreproc = null)
    {
        void proc(T2IRegisteredParam<string> param)
        {
            string val = Get(param) ?? "";
            val = generalPreproc is null ? val : generalPreproc(val);
            val = FillEmbedsInString(val, formatEmbed);
            ValuesInput[param.Type.ID] = val;
        }
        proc(T2IParamTypes.Prompt);
        proc(T2IParamTypes.NegativePrompt);
    }

    /// <summary>Special utility to process prompt inputs before the request is executed (to parse wildcards, embeddings, etc).</summary>
    public string ProcessPromptLike(T2IRegisteredParam<string> param)
    {
        string val = Get(param);
        if (val is null)
        {
            return "";
        }
        string fixedVal = val.Replace('\0', '\a').Replace("\a", "");
        long backupSeed = Get(T2IParamTypes.Seed) + Get(T2IParamTypes.VariationSeed, 0) + param.Type.Name.Length;
        long wildcardSeed = Get(T2IParamTypes.WildcardSeed, backupSeed);
        if (wildcardSeed > int.MaxValue)
        {
            wildcardSeed %= int.MaxValue;
        }
        if (wildcardSeed == -1)
        {
            wildcardSeed = Random.Shared.Next(int.MaxValue);
        }
        Random rand = new((int)wildcardSeed);
        string lowRef = fixedVal.ToLowerFast();
        string[] embeds = lowRef.Contains("<embed") ? Program.T2IModelSets["Embedding"].ListModelNamesFor(SourceSession).ToArray() : null;
        string[] loras = lowRef.Contains("<lora:") ? Program.T2IModelSets["LoRA"].ListModelNamesFor(SourceSession).Select(m => m.ToLowerFast()).ToArray() : null;
        PromptTagContext context = new() { Input = this, Random = rand, Param = param.Type.ID, Embeds = embeds, Loras = loras };
        fixedVal = ProcessPromptLike(fixedVal, context);
        if (fixedVal != val)
        {
            ExtraMeta[$"original_{param.Type.ID}"] = val;
        }
        return fixedVal.Replace("\a", "");
    }

    /// <summary>Special utility to process prompt inputs before the request is executed (to parse wildcards, embeddings, etc).</summary>
    public string ProcessPromptLike(string val, PromptTagContext context)
    {
        if (val is null)
        {
            return null;
        }
        string addBefore = "", addAfter = "";
        void processSet(Dictionary<string, Func<string, PromptTagContext, string>> set)
        {
            val = StringConversionHelper.QuickSimpleTagFiller(val, "<", ">", tag =>
            {
                (string prefix, string data) = tag.BeforeAndAfter(':');
                if (!string.IsNullOrWhiteSpace(data) && set.TryGetValue(prefix, out Func<string, PromptTagContext, string> proc))
                {
                    string result = proc(data, context);
                    if (result is not null)
                    {
                        if (result.StartsWithNull()) // Special case for preset tag modifying the current value
                        {
                            string cleanResult = result[1..];
                            if (cleanResult.StartsWith("preset:"))
                            {
                                cleanResult = cleanResult["preset:".Length..];
                                if (cleanResult.Contains("{value}"))
                                {
                                    addBefore += cleanResult.Before("{value}");
                                }
                                addAfter += cleanResult.After("{value}");
                                return "";
                            }
                        }
                        return result;
                    }
                }
                return $"<{tag}>";
            }, false, 0);
        }
        processSet(PromptTagProcessors);
        processSet(PromptTagPostProcessors);
        return addBefore + val + addAfter;
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
            return handler.Models.TryGetValue(best ?? name, out T2IModel actualModel) ? actualModel : new T2IModel() { Name = name };
        }
        if (param.IgnoreIf is not null && param.IgnoreIf == val)
        {
            ValuesInput.Remove(param.ID);
            return;
        }
        object obj = param.Type switch
        {
            T2IParamDataType.INTEGER => param.SharpType == typeof(long) ? long.Parse(val) : int.Parse(val),
            T2IParamDataType.DECIMAL => param.SharpType == typeof(double) ? double.Parse(val) : float.Parse(val),
            T2IParamDataType.BOOLEAN => bool.Parse(val),
            T2IParamDataType.TEXT or T2IParamDataType.DROPDOWN => val,
            T2IParamDataType.IMAGE => new Image(val, Image.ImageType.IMAGE, "png"),
            T2IParamDataType.IMAGE_LIST => val.Split('|').Select(v => new Image(v, Image.ImageType.IMAGE, "png")).ToList(),
            T2IParamDataType.MODEL => getModel(val),
            T2IParamDataType.LIST => val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            _ => throw new NotImplementedException()
        };
        if (param.SharpType == typeof(int))
        {
            obj = unchecked((int)(long)obj); // WTF. Yes this double-cast is needed. No I can't explain why. Ternaries are broken maybe?
        }
        if (param.SharpType == typeof(float))
        {
            obj = (float)(double)obj;
        }
        ValuesInput[param.ID] = obj;
        if (param.FeatureFlag is not null)
        {
            RequiredFlags.Add(param.FeatureFlag);
        }
    }

    /// <summary>Sets the direct raw value of a given parameter, without processing.</summary>
    public void Set<T>(T2IRegisteredParam<T> param, T val)
    {
        if (param.Type.Clean is not null)
        {
            Set(param.Type, val.ToString());
            return;
        }
        if (param.Type.IgnoreIf is not null && param.Type.IgnoreIf == $"{val}")
        {
            ValuesInput.Remove(param.Type.ID);
            return;
        }
        ValuesInput[param.Type.ID] = val;
        if (param.Type.FeatureFlag is not null)
        {
            RequiredFlags.Add(param.Type.FeatureFlag);
        }
    }
    
    /// <summary>Removes a param.</summary>
    public void Remove<T>(T2IRegisteredParam<T> param)
    {
        ValuesInput.Remove(param.Type.ID);
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
