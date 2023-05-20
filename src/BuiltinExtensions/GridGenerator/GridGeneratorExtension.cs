using Newtonsoft.Json.Linq;
using StableUI.Accounts;
using StableUI.Core;
using StableUI.DataHolders;

namespace StableUI.Builtin_GridGeneratorExtension;

/// <summary>Extension that adds a tool to generate grids of images.</summary>
public class GridGeneratorExtension : Extension
{
    public override void OnInit()
    {
        ScriptFiles.Add("Assets/grid_gen.js");
        StyleSheetFiles.Add("Assets/grid_gen.css");
    }

    public async Task GridGenAPIRoute(Session session, T2IParams user_input, JObject grid_axes)
    {
        List<T2IParams> variants = new();
    }
}
