# Making Extensions for StableSwarmUI

So, you want to make a Swarm extension, eh? You've come to the right place!

Here's some general info:

- Extensions can basically do anything, in fact much of Swarm's native functionality comes from built-in extensions.
- An extension is a folder inside `src/Extensions/`, for example `src/Extensions/MyExtension/...`
- Every extension has a root `.cs` C# class file that extends `Extension`, in a file named the same as the class, eg `src/Extensions/MyExtension/MyCoolExtensionName.cs` contains `public class MyCoolExtensionName : Extension`
- There's a variety of initialization points, and you can choose the one that fits your needs, and then register any usage/callbacks/etc.
- When writing a Swarm extension, you need to meet Swarm's code requirements -- most notably, that means you need to write code that won't explode if it's called from multiple threads (in most cases this won't be an issue, it's just something to consider when you're getting very advanced).
- All of Swarm is open source, including a pile of built-in-extensions ([see here](https://github.com/Stability-AI/StableSwarmUI/tree/master/src/BuiltinExtensions)), so you can reference any existing code to get examples of things
- Swarm uses C#, a compiled language, so it only recompiles if (A) you do so manually, (B) you run the `update` script in the swarm root, or (C) you launch using the `launch-dev` scripts (builds fresh every time). When working on extensions, you need to either use the dev scripts, or remember to run the update every time.
- You can add custom tabs by just making a folder inside your extension of `Tabs/Text2Image/` and inside of that put `Your Tab Name.html`
- See the [`Extension` class source here](https://github.com/Stability-AI/StableSwarmUI/blob/master/src/Core/Extension.cs) for more things you can do.
    - This has several different launch points (eg `OnInit`, `OnPreInit`, etc.) and some registration points (eg `ScriptFiles` and `StyleSheetFiles` to register custom web assets to the main page).

## Example: A Custom Comfy-Node-Backed Parameter

Save this file as `src/Extensions/MyExtension/MyCoolExtensionName.cs`:

```cs
using StableSwarmUI.Core;
using StableSwarmUI.Utils;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Builtin_ComfyUIBackend;
using Newtonsoft.Json.Linq;

// NOTE: Namespace must NOT contain "StableSwarmUI" (this is reserved for built-ins)
namespace MonkeysDocs.CoolExtensions.MyExtension;

// NOTE: Classname must match filename
public class MyCoolExtensionName : Extension // extend the "Extension" class in Swarm Core
{
    // Generally define parameters as "public static" to make them easy to access in other code, actual registration is done in OnInit
    public static T2IRegisteredParam<int> MyCoolParam;

    public static T2IParamGroup MyCoolParamGroup;

    // OnInit is called when the extension is loaded, and is the general place to register most things
    public override void OnInit()
    {
        Logs.Init("Wow my cool extension is doing a thing"); // Use "Logs" for any/all logging.
        MyCoolParamGroup = new("My Cool Param Group", Toggles: false, Open: false, IsAdvanced: true);
        // Note that parameter name in code and registration should generally match (for simple clarity).
        MyCoolParam = T2IParamTypes.Register<int>(new("My Cool Param", "Some description about my cool parameter here. This demo blurs the final image.",
            "10", Toggleable: true, Group: MyCoolParamGroup, FeatureFlag: "comfyui" // "comfyui" feature flag for parameters that require ComfyUI
            // Check your IDE's completions here, there's tons of additional options. Look inside the T2IParamTypes to see how other params are registered.
            ));
        // AddStep for custom Comfy steps. Can also use AddModelGenStep for custom model configuration steps.
        WorkflowGenerator.AddStep(g =>
        {
            // Generally always check that your parameter exists before doing anything (so you don't infect unrelated generations unless the user wants your feature running)
            if (g.UserInput.TryGet(MyCoolParam, out int myParamNumber))
            {
                // Create the node we want...
                string shiftNode = g.CreateNode("ImageBlur", new JObject()
                {
                    // And configure all the inputs to that node...
                    ["image"] = g.FinalImageOut, // Take in the prior final image value
                    ["blur_radius"] = myParamNumber,
                    ["sigma"] = 5
                });
                // And then make sure its result actually gets used. The final save image uses 'FinalImageOut' to identify what to save, so just update that.
                g.FinalImageOut = [shiftNode, 0]; // (Note the 0 is because some nodes have multiple outputs, so 0 means use the first output)
            }
            // The priority value determines where in the workflow this will process.
            // You can technically just use a late priority and then just modify the workflow at will, but it's best to run at the appropriate time.
            // Check the source of WorkflowGenerator to see what priorities are what.
            // In this case, the final save image step is at priority of "10", so we run at "9", ie just before that.
            // (You can use eg 9.5 or 9.999 if you think something else is running at 9 and you need to be after it).
        }, 9);
    }
}
```

Then:
- rebuild and launch Swarm (run the update file, or launch-dev file)
- Open the UI, enable `Display Advanced Options`, find `My Cool Param Group`, inside it enable `My Cool Param`
- Generate an image and observe that it's now generating blurred images!
- Then go back and modify the code to do whatever you actually need.
- Then maybe publish your extension on GitHub for other people to use :D
    - Just `git init` inside the `src/Extension/MyExtension` folder and publish that on GitHub, others can simply `git clone` your repo, then run the update script and enjoy it.
