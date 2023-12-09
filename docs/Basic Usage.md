# Basic Usage of StableSwarmUI

So you want to know how to get started with StableSwarmUI, huh? It's easy!

For the most part, just download the installer and follow the instructions on screen. Everything explains itself, even the settings and parameters all have `?` clickables that explain what they do!

Nonetheless, here's a step-by-step you can follow:

## Installing

Step one: [Install StableSwarmUI](/README.md#installing-on-windows).

Once you've ran the basic program-installation, if all went well, it will open a web interface to select basic install settings.
- Agree to the SD license
- Pick a theme (I think default is best, but you got options)
- Pick who the UI is for (usually just Yourself or Yourself on LAN)
- Pick what backend(s) to install. If you already have ComfyUI or another backend you can skip this - if not, pick one. I recommend ComfyUI for local usage.
- Pick any model(s) you want to download. If you already have some you can skip this, if not, I recommend SDXL 1.0.
- Confirm you want the settings you selected, and install.

Once this is done, it should automatically redirect you to the main interface.

(You can close the server at any time by just closing that console window it pulls up, and you can start it again via the desktop icon, or the `launch` script in the folder).

## Configuring

If you have pre-existing Stable Diffusion files, you'll want to configure settings a bit. If not, the defaults are probably fine.
- If you have an Auto WebUI or ComfyUI folder with models in it, go to the `Server` tab then `Server Configuration` and set `ModelRoot` to the path to your UI's `models` dir, and set `SDModelFolder` to `Stable-diffusion` for Auto WebUI or `checkpoints` for ComfyUI.
- Be sure to click the 'Save' button at the bottom when you're done (only visible if you've edited any settings)

![img](/docs/images/servermodelpath.png)

## Your First Image

- Open the `Generate` tab at the top
- At the bottom, there's a `Models` tab, click that
    - on the left, there's a folder tree. Click into the folder you want. If this is a fresh install and you downloaded the official models, there's exactly one OfficialStableDiffusion folder here.
    - there's now a model listing. Click the icon of the model you want. (You can also change the view between cards/thumbnails/list)
    - You can also click the name of the model in the info bar just above those bottom tabs, and you'll get a quick-dropdown list of models, if you prefer that.
    - You can also click the bar separating the bottom tabs from the main area above it, and drag it down, to get it out of your way.

![img](/docs/images/draggable.png)

- Now, in the bottom-center, there's a text box that says `Type your prompt here...` - type something nice into this box, like `a photo of a cat`
- Next either hit Enter, or click the purple `Generate` button

![img](/docs/images/yourfirstprompt.png)

- If all went well, you should have a nice picture of a cat in the center of your screen!

If it didn't go well, ... well it's alpha software, hopefully there's an error message telling you what went wrong. If you can't figure it out, open an [issue here](https://github.com/Stability-AI/StableSwarmUI/issues) or ask on [discord](https://discord.gg/stablediffusion) in the `stableswarmui` channel.

## Using The SDXL Refiner

So, you want *refined* images, huh? Well, if the base isn't enough, and you downloaded the refiner, you can put it to use!

- Just open the `Refiner` parameter group (you might have to scroll down if your screen is small - it's right below `Init Image`).
- Enable the `Refiner Model` parameter and select your model (it lists recognized refiners at the top, you can use other models too if you want)
- Set the other parameters however you want.  Click the `?` if you're not sure what a parameter does.
- Click Generate! Yup that's literally all you have to do. Easy, huh?
- You can turn it off at any time by toggling the toggler on the group (right next to where it says `Refiner` as the group label).

![img](/docs/images/refiners.png)

## Getting Advanced: Playing With Parameters

StableSwarmUI is designed on the principle of exposing all the parameters to you, but making them approachable. To that end:
- All the parameters are grouped. You can toggle groups open/closed, and where relevant there's sliders to toggle groups on/off.
- All the parameters have a lil purple `?` icon next to them. Clicking this will open a popup with an explanation of the parameter and some examples.
- When in doubt, with most parameters, just play with them and see what happens. Lock in your `Seed` to a constant one before trying to avoid unrelated changes.

## More Than Text: Playing with Prompts

Prompting is, primarily, just text input. However, there are some special options also available (on both Prompt and Negative Prompt):
- Prompt weighting, eg `an (orange) cat` or `an (orange:1.5) cat`. Anything in `(parens)` has its weighting modified - meaning, the model will pay more attention to that part of the prompt. Values above `1` are more important, values below `1` (eg `0.5`) are less important.
    - You can also hold Control and press the up/down arrow keys to change the weight of selected text.
    - Note: the way prompt weights are understood is different depending on backend.
- If using SDXL or UnClip, you can use *ReVision* by just dragging an image into the prompt box. This will have the model interpret the image (using ClipVision) and include it in the prompt.
- You can use the syntax `<random:red, blue, purple>` to randomly select from a list for each gen
    - This random is seeded by the main seed - so if you have a static seed, this won't change.
    - You can use `,` to separate the entries, or `|`, or `||`. Whichever is most unique gets used - so if you want random options with `,` in them, just use `|` as a separator, and `,` will be ignored (eg `<random:red|blue|purple>`).
    - An entry can contain the syntax of eg `1-5` to automatically select a number from 1 to 5. For example, `<random:1-3, blue>` will give back any of: `1`, `2`, `3`, or `blue`.
    - You can repeat random choices via `<random[1-3]:red, blue, purple>` which might return for example `red blue` or `red blue purple` or `blue`.
        - You can use a comma at the end like `random[1-3,]` to specify the output should have a comma eg `red, blue`.
        - This will avoid repetition, unless you have a large count than number of options.
- You can use the syntax `<wildcard:my/wildcard/name>` to randomly select from a wildcard file, which is basically a pre-saved text file of random options, 1 per line.
    - Edit these in the UI at the bottom in the "Wildcards" tab.
    - You can also import wildcard files from other UIs (ie text file collections) by just adding them into `Data/Wildcards` folder.
    - This supports the same syntax as `random` to get multiple, for example `<wildcard[1-3]:animals>` might return `cat dog` or `elephant leopard dog`.
- You can use the syntax `<repeat:3, cat>` to get the word "cat" 3 times in a row (`cat cat cat`).
    - You can use for example like `<repeat:1-3, <random:cat, dog>>` to get between 1 and 3 copies of either `cat` or `dog`, for example it might return `cat dog cat`.
- You can use `<embed:filename>` to use a Textual Inversion embedding anywhere.
- You may use `<lora:filename:weight>` to enable a LoRA
    - Note that it's generally preferred to use the GUI at the bottom of the page to select loras
    - Note that position within the prompt doesn't matter, loras are not actually a prompt feature, this is just a convenience option for users used to Auto WebUI.
- You can use `<preset:presetname>` to inject a preset.
    - GUI is generally preferred for LoRAs, this is available to allow dynamically messing with presets (eg `<preset:<random:a, b>>`)
- You can use `<segment:texthere>` to automatically refine part of the image using CLIP Segmentation.
    - Or `<segment:texthere,creativity,threshold>` - where creativity is inpaint strength, and threshold is segmentation minimum threshold - for example, `<segment:face,0.8,0.5>` - defaults to 0.6 creativity, 0.5 threshold.
    - See [the feature announcement](https://github.com/Stability-AI/StableSwarmUI/discussions/11#discussioncomment-7236821) for details.
- You can use `<clear:texthere>` to automatically clear parts of an image to transparent. This uses the same input format as `segment` (above) (for obvious reasons, this requires PNG not JPG).
    - For example, `<clear:background>` to clear the background.

## We Gotta Go Faster: Add More Backends

You want more images more faster right now hurry up and tell me how quick quick quick? The Backends tab holds the key to your speedy needs.
- First, find some GPUs you can use.
    - The obvious choice is additional GPUs in your PC if you have any, but who has those? (If you have that ... first of all, Congrats! That's awesome! Second, you can absolutely use that)
    - Do you have other computers in your house? Maybe a second machine in the living room, or a laptop sitting around? If there's GPUs in em, they'll do!
    - Fan of Google Colab? That'll work!
    - Got some cash to spare? Rent a GPU server online (eg vastai, runpod, aws, etc).
    - Got close friends with GPUs? If you can figure out the network routing (and the interpersonal agreements), you can use those!
- Go to the `Server` -> `Backends` page
- Pick the backend type that you want, and click the button to add it.
    - For remote hosts, you'll want to set up StableSwarmUI over there, get a remote connection working, and then add `Swarm-API-Backend`, and give the URL to it.
    - For more GPUs in your one machine, you can eg use multiple `ComfyUI Self-Starting` and just change the GPU ID for each.
- If it doesn't error out, you can immediately start using it.
    - When you generate images, your first generation goes to the first backend, your second to the second one, etc.
    - If you generate very slowly, you might only use one backend. If you release a big batch, or spam the Generate button, or use a grid, or etc. it will spread across all backends automatically.
- You can of course edit or delete backends at will from that page. They will persist across restarts automatically.
- If you use custom comfy workflows, note that that comfy workflow tab only uses your first comfy instance - you'll only use all available backends once you use the `Use This Workflow` button and go back to the main `Generate` tab.

## Testing Like A Pro: The Grid Generator

What's the fun in testing changes one by one, when you can unleash your machine to test all of them and lay it out clearly for you? This is where the Grid Generator comes in!
- Go to the `Tools` tab at the bottom
- In the dropdown, select `Grid Generator`
- Optionally name your grid
- Use the dropdown to select an axis you want to compare
- Fill it with values yourself, or you can click the button on the right to fill with examples (where applicable)
- You'll notice a new axis box is available automatically. You can add as many as you want.
- When you're ready, click `Generate Grid` at the top - it replaced your `Generate Image` button
    - To get `Generate Image` back, swap the tool back to the blank space at the top of the tools list
- Click the output link to view your grid as it comes

![img](/docs/images/grids.png)

## Take It Further: Advanced Usage

View more info about advanced usages in the [Advanced Usage](/docs/Advanced%20Usage.md) doc.
