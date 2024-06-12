using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;

namespace StableSwarmUI.Text2Image;

/// <summary>Represents user-input for a Text2Image request.</summary>
public class T2IParamInput
{
    /// <summary>Special handlers for any special logic to apply post-loading a param input.</summary>
    public static List<Action<T2IParamInput>> SpecialParameterHandlers =
    [
        input =>
        {
            if (!input.TryGet(T2IParamTypes.Seed, out long seed) || seed == -1)
            {
                input.Set(T2IParamTypes.Seed, Random.Shared.Next());
            }
        },
        input =>
        {
            if (input.TryGet(T2IParamTypes.VariationSeed, out long seed) && seed == -1)
            {
                input.Set(T2IParamTypes.VariationSeed, Random.Shared.Next());
            }
        },
        input =>
        {
            if (input.TryGet(T2IParamTypes.RawResolution, out string res))
            {
                (string widthText, string heightText) = res.BeforeAndAfter('x');
                int width = int.Parse(widthText.Trim());
                int height = int.Parse(heightText.Trim());
                input.Set(T2IParamTypes.Width, width);
                input.Set(T2IParamTypes.Height, height);
                input.Remove(T2IParamTypes.AltResolutionHeightMult);
            }
        }
    ];

    public class PromptTagContext
    {
        public Random Random;

        public T2IParamInput Input;

        public string Param;

        public string[] Embeds, Loras;

        public int SectionID = 0;

        public int Depth = 0;

        /// <summary>If the current syntax usage has a pre-data block, it will be here. This will be null otherwise.</summary>
        public string PreData;

        public string RawCurrentTag;

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
    public static Dictionary<string, Func<string, PromptTagContext, string>> PromptTagProcessors = [];

    /// <summary>Mapping of prompt tag prefixes, to allow for registration of custom prompt tags - specifically post-processing like lora (which remove from prompt and get read elsewhere).</summary>
    public static Dictionary<string, Func<string, PromptTagContext, string>> PromptTagPostProcessors = [];

    /// <summary>Mapping of prompt tag prefixes, to strings intended to allow for estimating token count.</summary>
    public static Dictionary<string, Func<string, string>> PromptTagLengthEstimators = [];

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

    public static (int, string) InterpretPredataForRandom(string prefix, string preData, string data)
    {
        int count = 1;
        string separator = " ";
        if (preData is not null)
        {
            if (preData.EndsWithFast(','))
            {
                separator = ", ";
                preData = preData[0..^1];
            }
            double? countVal = InterpretNumber(preData);
            if (!countVal.HasValue)
            {
                Logs.Warning($"Random input '{prefix}[{preData}]:{data}' has invalid predata count (not a number) and will be ignored.");
                return (0, null);
            }
            count = (int)countVal.Value;
        }
        return (count, separator);
    }

    static T2IParamInput()
    {
        PromptTagProcessors["random"] = (data, context) =>
        {
            (int count, string partSeparator) = InterpretPredataForRandom("random", context.PreData, data);
            if (partSeparator is null)
            {
                return null;
            }
            string separator = data.Contains("||") ? "||" : (data.Contains('|') ? "|" : ",");
            string[] rawVals = data.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (rawVals.Length == 0)
            {
                Logs.Warning($"Random input '{data}' is empty and will be ignored.");
                return null;
            }
            string result = "";
            List<string> vals = [.. rawVals];
            for (int i = 0; i < count; i++)
            {
                int index = context.Random.Next(vals.Count);
                string choice = vals[index];
                if (TryInterpretNumberRange(choice, out string number))
                {
                    return number;
                }
                result += context.Parse(choice).Trim() + partSeparator;
                if (vals.Count == 1)
                {
                    vals = [.. rawVals];
                }
                else
                {
                    vals.RemoveAt(index);
                }
            }
            return result.Trim();
        };
        PromptTagLengthEstimators["random"] = (data) =>
        {
            string separator = data.Contains("||") ? "||" : (data.Contains('|') ? "|" : ",");
            string[] rawVals = data.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            int longest = 0;
            string longestStr = "";
            foreach (string val in rawVals)
            {
                string interp = ProcessPromptLikeForLength(val);
                if (interp.Length > longest)
                {
                    longest = interp.Length;
                    longestStr = interp;
                }
            }
            return longestStr;
        };
        PromptTagProcessors["alternate"] = (data, context) =>
        {
            string separator = data.Contains("||") ? "||" : (data.Contains('|') ? "|" : ",");
            string[] rawVals = data.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (rawVals.Length == 0)
            {
                Logs.Warning($"Alternate input '{data}' is empty and will be ignored.");
                return null;
            }
            return $"[{rawVals.JoinString("|")}]";
        };
        PromptTagLengthEstimators["alternate"] = PromptTagLengthEstimators["random"];
        PromptTagProcessors["fromto"] = (data, context) =>
        {
            double? stepIndex = InterpretNumber(context.PreData);
            if (!stepIndex.HasValue)
            {
                Logs.Warning($"FromTo input 'fromto[{context.PreData}]:{data}' has invalid predata step-index (not a number) and will be ignored.");
                return null;
            }
            string separator = data.Contains("||") ? "||" : (data.Contains('|') ? "|" : ",");
            string[] rawVals = data.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (rawVals.Length != 2)
            {
                Logs.Warning($"Alternate input '{data}' is invalid (len=${rawVals.Length}, should be 2) and will be ignored.");
                return null;
            }
            return $"[{rawVals.JoinString(":")}:{stepIndex}]";
        };
        PromptTagLengthEstimators["fromto"] = PromptTagLengthEstimators["random"];
        PromptTagProcessors["wildcard"] = (data, context) =>
        {
            (int count, string partSeparator) = InterpretPredataForRandom("random", context.PreData, data);
            if (partSeparator is null)
            {
                return null;
            }
            string card = T2IParamTypes.GetBestInList(data, WildcardsHelper.ListFiles);
            if (card is null)
            {
                Logs.Warning($"Wildcard input '{data}' does not match any wildcard file and will be ignored.");
                return null;
            }
            WildcardsHelper.Wildcard wildcard = WildcardsHelper.GetWildcard(card);
            List<string> usedWildcards = context.Input.ExtraMeta.GetOrCreate("used_wildcards", () => new List<string>()) as List<string>;
            usedWildcards.Add(card);
            string result = "";
            List<string> vals = [.. wildcard.Options];
            for (int i = 0; i < count; i++)
            {
                int index = context.Random.Next(vals.Count);
                string choice = vals[index];
                result += context.Parse(choice).Trim() + partSeparator;
                if (vals.Count == 1)
                {
                    vals = [.. wildcard.Options];
                }
                else
                {
                    vals.RemoveAt(index);
                }
            }
            return result.Trim();
        };
        PromptTagLengthEstimators["wildcard"] = (data) =>
        {
            string card = T2IParamTypes.GetBestInList(data, WildcardsHelper.ListFiles);
            if (card is null)
            {
                return "";
            }
            WildcardsHelper.Wildcard wildcard = WildcardsHelper.GetWildcard(card);
            int longest = 0;
            string longestStr = "";
            foreach (string val in wildcard.Options)
            {
                string interp = ProcessPromptLikeForLength(val);
                if (interp.Length > longest)
                {
                    longest = interp.Length;
                    longestStr = interp;
                }
            }
            return longestStr;
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
        PromptTagLengthEstimators["repeat"] = (data) =>
        {
            (string count, string value) = data.BeforeAndAfter(',');
            double? countVal = InterpretNumber(count);
            if (!countVal.HasValue)
            {
                return "";
            }
            string interp = ProcessPromptLikeForLength(value);
            string result = "";
            for (int i = 0; i < countVal.Value; i++)
            {
                result += interp + " ";
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
        PromptTagLengthEstimators["preset"] = (data) =>
        {
            return "";
        };
        PromptTagProcessors["embed"] = (data, context) =>
        {
            data = context.Parse(data);
            context.Embeds ??= [.. Program.T2IModelSets["Embedding"].ListModelNamesFor(context.Input.SourceSession)];
            string want = data.ToLowerFast().Replace('\\', '/');
            string matched = T2IParamTypes.GetBestModelInList(want, context.Embeds);
            if (matched is null)
            {
                Logs.Warning($"Embedding '{want}' does not exist and will be ignored.");
                return "";
            }
            List<string> usedEmbeds = context.Input.ExtraMeta.GetOrCreate("used_embeddings", () => new List<string>()) as List<string>;
            usedEmbeds.Add(T2IParamTypes.CleanModelName(matched));
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
            context.Loras ??= [.. Program.T2IModelSets["LoRA"].ListModelNamesFor(context.Input.SourceSession)];
            string matched = T2IParamTypes.GetBestModelInList(lora, context.Loras);
            if (matched is not null)
            {
                List<string> loraList = context.Input.Get(T2IParamTypes.Loras);
                List<string> weights = context.Input.Get(T2IParamTypes.LoraWeights);
                List<string> confinements = context.Input.Get(T2IParamTypes.LoraSectionConfinement);
                if (loraList is null)
                {
                    loraList = [];
                    weights = [];
                }
                loraList.Add(matched);
                weights.Add(strength.ToString());
                context.Input.Set(T2IParamTypes.Loras, loraList);
                context.Input.Set(T2IParamTypes.LoraWeights, weights);
                if (context.SectionID > 0)
                {
                    if (confinements is null)
                    {
                        confinements = [];
                        for (int i = 0; i < loraList.Count - 1; i++)
                        {
                            confinements.Add("0");
                        }
                    }
                    Logs.Verbose($"LoRA {lora} confined to section {context.SectionID}.");
                    confinements.Add($"{context.SectionID}");
                    context.Input.Set(T2IParamTypes.LoraSectionConfinement, confinements);
                }
                return "";
            }
            Logs.Warning($"Lora '{lora}' does not exist and will be ignored.");
            return null;
        };
        PromptTagPostProcessors["segment"] = (data, context) =>
        {
            context.SectionID++;
            return $"<{context.RawCurrentTag}//cid={context.SectionID}>";
        };
        PromptTagPostProcessors["object"] = PromptTagPostProcessors["segment"];
        PromptTagPostProcessors["region"] = PromptTagPostProcessors["segment"];
        PromptTagProcessors["break"] = (data, context) =>
        {
            return "<break>";
        };
        PromptTagLengthEstimators["break"] = (data) =>
        {
            return "<break>";
        };
        PromptTagLengthEstimators["embed"] = PromptTagLengthEstimators["preset"];
        PromptTagLengthEstimators["embedding"] = PromptTagLengthEstimators["preset"];
        PromptTagLengthEstimators["lora"] = PromptTagLengthEstimators["preset"];
    }

    /// <summary>The raw values in this input. Do not use this directly, instead prefer:
    /// <see cref="Get{T}(T2IRegisteredParam{T})"/>, <see cref="TryGet{T}(T2IRegisteredParam{T}, out T)"/>,
    /// <see cref="Set{T}(T2IRegisteredParam{T}, string)"/>.</summary>
    public Dictionary<string, object> ValuesInput = [];

    /// <summary>Extra data to store in metadata.</summary>
    public Dictionary<string, object> ExtraMeta = [];

    /// <summary>A set of feature flags required for this input.</summary>
    public HashSet<string> RequiredFlags = [];

    /// <summary>The session this input came from.</summary>
    public Session SourceSession;

    /// <summary>Interrupt token from the session.</summary>
    public CancellationToken InterruptToken;

    /// <summary>List of reasons this input did not match backend requests, if any.</summary>
    public HashSet<string> RefusalReasons = [];

    /// <summary>Construct a new parameter input handler for a session.</summary>
    public T2IParamInput(Session session)
    {
        SourceSession = session;
        InterruptToken = session is null ? new CancellationTokenSource().Token : session.SessInterrupt.Token;
        ExtraMeta["swarm_version"] = Utilities.Version;
        ExtraMeta["date"] = DateTime.Now.ToString("yyyy-MM-dd");
    }

    /// <summary>Gets the desired image width, automatically using alt-res parameter if needed.</summary>
    public int GetImageHeight()
    {
        if (TryGet(T2IParamTypes.RawResolution, out string res))
        {
            return int.Parse(res.After('x'));
        }
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
        toret.ValuesInput = new Dictionary<string, object>(ValuesInput.Count);
        foreach ((string key, object val) in ValuesInput)
        {
            object useVal = val;
            if (useVal is List<string> strs) { useVal = new List<string>(strs); }
            else if (useVal is List<Image> imgs) { useVal = new List<Image>(imgs); }
            else if (useVal is List<T2IModel> models) { useVal = new List<T2IModel>(models); }
            toret.ValuesInput[key] = useVal;
        }
        toret.ExtraMeta = new Dictionary<string, object>(ExtraMeta);
        toret.RequiredFlags = new HashSet<string>(RequiredFlags);
        return toret;
    }

    public static object SimplifyParamVal(object val)
    {
        if (val is Image img)
        {
            return img.AsBase64;
        }
        else if (val is List<Image> imgList)
        {
            return imgList.Select(img => img.AsBase64).JoinString("|");
        }
        else if (val is List<string> strList)
        {
            return strList.JoinString(",");
        }
        else if (val is List<T2IModel> modelList)
        {
            return modelList.Select(m => T2IParamTypes.CleanModelName(m.Name)).JoinString(",");
        }
        else if (val is T2IModel model)
        {
            return T2IParamTypes.CleanModelName(model.Name);
        }
        else if (val is string str)
        {
            return FillEmbedsInString(str, e => $"<embed:{e}>");
        }
        return val;
    }

    /// <summary>Generates a JSON object for this input that can be fed straight back into the Swarm API.</summary>
    public JObject ToJSON()
    {
        JObject result = [];
        foreach ((string key, object val) in ValuesInput)
        {
            result[key] = JToken.FromObject(SimplifyParamVal(val));
        }
        return result;
    }

    /// <summary>Generates a metadata JSON object for this input and the given set of extra parameters.</summary>
    public JObject GenMetadataObject()
    {
        JObject output = [];
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
                val = T2IParamTypes.CleanModelName(model.Name);
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
        PromptTagContext context = new() { Input = this, Random = rand, Param = param.Type.ID };
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
                string preData = null;
                if (prefix.EndsWith(']') && prefix.Contains('['))
                {
                    (prefix, preData) = prefix.BeforeLast(']').BeforeAndAfter('[');
                }
                context.RawCurrentTag = tag;
                context.PreData = preData;
                Logs.Verbose($"[Prompt Parsing] Found tag {val}, will fill... prefix = '{prefix}', data = '{data}', predata = '{preData}'");
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

    public static string ProcessPromptLikeForLength(string val)
    {
        if (val is null)
        {
            return null;
        }
        void processSet(Dictionary<string, Func<string, string>> set)
        {
            val = StringConversionHelper.QuickSimpleTagFiller(val, "<", ">", tag =>
            {
                (string prefix, string data) = tag.BeforeAndAfter(':');
                string preData = null;
                if (prefix.EndsWith(']') && prefix.Contains('['))
                {
                    (prefix, preData) = prefix.BeforeLast(']').BeforeAndAfter('[');
                }
                if (!string.IsNullOrWhiteSpace(data) && set.TryGetValue(prefix, out Func<string, string> proc))
                {
                    string result = proc(data);
                    if (result is not null)
                    {
                        return result;
                    }
                }
                return $"<{tag}>";
            }, false, 0);
        }
        processSet(PromptTagLengthEstimators);
        return val;
    }

    /// <summary>Gets the raw value of the parameter, if it is present, or null if not.</summary>
    public object GetRaw(T2IParamType param)
    {
        return ValuesInput.GetValueOrDefault(param.ID);
    }

    /// <summary>Gets the value of the parameter, if it is present, or default if not.</summary>
    public T Get<T>(T2IRegisteredParam<T> param) => Get(param, default, true);

    /// <summary>Gets the value of the parameter, if it is present, or default if not.</summary>
    public T Get<T>(T2IRegisteredParam<T> param, T defVal, bool autoFixDefault = false)
    {
        if (!ValuesInput.TryGetValue(param.Type.ID, out object val))
        {
            if (autoFixDefault && !string.IsNullOrWhiteSpace(param.Type.Default))
            {
                Set(param.Type, param.Type.Default);
                T result = Get(param, defVal, false);
                Remove(param);
                return result;
            }
            return defVal;
        }
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
            string best = T2IParamTypes.GetBestModelInList(name.Replace('\\', '/'), [.. handler.Models.Keys]);
            return handler.Models.TryGetValue(best ?? name, out T2IModel actualModel) ? actualModel : null;
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
        if (obj is null)
        {
            Logs.Debug($"Ignoring input to parameter {param.ID} because the value maps to null.");
            return;
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
            Set(param.Type, val is List<string> list ? list.JoinString(",") : val.ToString());
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

    /// <summary>Makes sure the input has valid seed inputs and other special parameter handlers.</summary>
    public void ApplySpecialLogic()
    {
        foreach (Action<T2IParamInput> handler in SpecialParameterHandlers)
        {
            handler(this);
        }
    }

    /// <summary>Returns a simple text representation of the input data.</summary>
    public override string ToString()
    {
        static string stringifyVal(object obj)
        {
            string val = $"{SimplifyParamVal(obj)}";
            if (val.Length > 256)
            {
                val = val[..256] + "...";
            }
            return val;
        }
        return $"T2IParamInput({string.Join(", ", ValuesInput.Select(x => $"{x.Key}: {stringifyVal(x.Value)}"))})";
    }
}
