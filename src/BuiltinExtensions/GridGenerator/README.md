# Grid Generator Extension

Infinite-dimensional multi-axis image grid generator tool for StableSwarmUI.

### Concept

Operates as a "Tool" within the "Tools" UI, built into Swarm by default as an official reference Extension, lets you generate grids of images to compare prompts or parameters.

- Grids are infinite dimensional, you can add as many axes as you want.
- Grids display as a webpage that you can open and select axis display settings dynamically. You can save an image from the grid view.
- You can opt to generate "Just Images", a "Grid Image", or a "Web Page"
    - "Just Images" as the name implies, just gives you images. They are stored in the normal image path.
    - "Grid Image" gives you one single final image at the end. This currently only works up to 3 axes (X, Y, and Y2), and will fail with more.
    - "Web Page" will generate a special web page with a dynamic advanced grid live-viewer, that lets you reorganize the view freely, and display up to 4 axes at a time (and easily swap to other ones).
- You can save/load grid configurations at will to reuse them.
- **WARNING**: The time it takes to generate a grid grows exponentially.
    - Lets say it takes you 1 second per image. You have one axis with 10 values... that's 10 seconds. Now you add another axis with 10 values... that's now 10\*10 = 100 seconds. Now you add a third axis with 10 values... now it takes 1000 seconds (17 minutes). Add a fourth and you're at 10k seconds (3 hours). Keep going and soon you'll be taking days, weeks, ... past a certain scale, it doesn't make sense unless you have several GPUs backing your generations to counteract the scaling.

### Tricks

- When using numbered parameters, for example `Seed`, you can input `..` between numbers to automatically fill that space, for example `1, 2, .., 10`
    - Must have two numbers before (to identify the start and step), and one number after (to identify the end)
    - For example: `1, 2, .., 5` fills to `1, 2, 3, 4, 5`
    - For example: `1, 3, 5, 6, 6.5, .., 9, 11, 13` fills to `1, 3, 5, 6, 6.5, 7, 7.5, 8, 8.5, 9, 11, 13`
- Any parameters may have `SKIP:` in front of them (all caps!) to skip that value.
    - For example, `1, 2, SKIP: 3, 4` will output a grid that has all of 1,2,3,4, but only has images in 1,2,4.
        - This is useful particularly for when you're reusing grid pages and want to leave a placeholder, or overwrite some images but leave the rest as they were.
- Want to use prompts that have a `,` in them? Just separate by `||` instead.
    - For example: `a cat, red || a cat, blue` will be parsed correctly as containing two text prompts.
- `[Grid Gen] Prompt Replace` is pretty powerful.
    - If your prompt is `a photo of a cat`, you can do `cat, dog, wolf` to generate `a photo of a cat`, `a photo of a dog`, `a photo of a wolf`
    - But you can also do `cat=cat, cat=dog, photo=drawing` to generate `a photo of a cat`, `a photo of a dog`, `a drawing of a cat`
    - But *also* you can have prompt `a photo of a cat <lora:mylora>` and then prompt replace `mylora, myotherlora, mythirdlora`
        - Or you can do `a photo of a cat` and then `cat, cat <lora:mycatlora>, dog, dog <lora:mydoglora>`
        - You can potentially do this with any parameter or combination of parameters you want by building Presets in the Presets tab and then using `<preset:whatever>` in the prompt replacement
- `[Grid Gen] Presets` is a list
    - You can do `mypreset || mypreset, mypreset2 || mypreset2` to compare two presets and also the combination thereof.

### History

The first version of this tool was [Infinity Grid Generator for Automatic1111's Stable-Diffusion-WebUI](https://github.com/mcmonkeyprojects/sd-infinity-grid-generator-script). It has a very special place in my heart as it was used by Stability employees, which was a key factor that led to me (Alex "mcmonkey" Goodwin) getting hired by Stability, and being given the opportunity to build bigger-and-better tools like StableSwarmUI!

That version had custom config files to enable added metadata (titles, descriptions, etc) for grid axis values - this will eventually be reimplemented to the Swarm version.
