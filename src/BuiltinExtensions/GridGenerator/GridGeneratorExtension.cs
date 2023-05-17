using StableUI.Core;

namespace StableUI.Builtin_GridGeneratorExtension;

/// <summary>Extension that adds a tool to generate grids of images.</summary>
public class GridGeneratorExtension : Extension
{
    public override void OnInit()
    {
        ScriptFiles.Add("Assets/grid_gen.js");
        StyleSheetFiles.Add("Assets/grid_gen.css");
    }
}
