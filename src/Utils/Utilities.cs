using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace StableUI.Utils;

public static class Utilities
{
    public static JObject StreamToJSON(Stream stream)
    {
        using StreamReader bodyReader = new(stream, Encoding.UTF8);
        using JsonTextReader jsonReader = new(bodyReader);
        // TODO: Input limiters to prevent malicious inputs (eg RAM overload)
        return JObject.Load(jsonReader);
    }
}
