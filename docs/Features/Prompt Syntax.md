# Advanced Prompt Syntax

### Weighting

- Prompt weighting, eg `an (orange) cat` or `an (orange:1.5) cat`. Anything in `(parens)` has its weighting modified - meaning, the model will pay more attention to that part of the prompt. Values above `1` are more important, values below `1` (eg `0.5`) are less important.
    - You can also hold Control and press the up/down arrow keys to change the weight of selected text.
    - Note: the way prompt weights are understood is different depending on backend.

### Alternating

- You can use `<alternate:cat, dog>` to alternate every step between `cat` and `dog`, creating a merge/mixture of the two concepts.
    - Similar to `random` you can instead use `|` or `||` to separate entries. You can have as many unique words as you want, eg `<alternate:cat, dog, horse, wolf, taco>` has 5 words so it will cycle through them every 5 steps.

### From-To

- You can use `<fromto[#]:before, after>` to swap between two phrases after a certain timestep.
    - The timestep can be like `10` for step 10, or like `0.5` for halfway-through.
    - Similar to `random` you can instead use `|` or `||` to separate entries. Must have exactly two entries.
    - For example, `<fromto[0.5]:cat, dog>` swaps from `cat` to `dog` halfway through a generation.

### Random

- You can use the syntax `<random:red, blue, purple>` to randomly select from a list for each gen
    - This random is seeded by the main seed - so if you have a static seed, this won't change.
    - You can use `,` to separate the entries, or `|`, or `||`. Whichever is most unique gets used - so if you want random options with `,` in them, just use `|` as a separator, and `,` will be ignored (eg `<random:red|blue|purple>`).
    - An entry can contain the syntax of eg `1-5` to automatically select a number from 1 to 5. For example, `<random:1-3, blue>` will give back any of: `1`, `2`, `3`, or `blue`.
    - You can repeat random choices via `<random[1-3]:red, blue, purple>` which might return for example `red blue` or `red blue purple` or `blue`.
        - You can use a comma at the end like `random[1-3,]` to specify the output should have a comma eg `red, blue`.
        - This will avoid repetition, unless you have a large count than number of options.

### Wildcards

- You can use the syntax `<wildcard:my/wildcard/name>` to randomly select from a wildcard file, which is basically a pre-saved text file of random options, 1 per line.
    - Edit these in the UI at the bottom in the "Wildcards" tab.
    - You can also import wildcard files from other UIs (ie text file collections) by just adding them into `Data/Wildcards` folder.
    - This supports the same syntax as `random` to get multiple, for example `<wildcard[1-3]:animals>` might return `cat dog` or `elephant leopard dog`.

### Repeat

- You can use the syntax `<repeat:3, cat>` to get the word "cat" 3 times in a row (`cat cat cat`).
    - You can use for example like `<repeat:1-3, <random:cat, dog>>` to get between 1 and 3 copies of either `cat` or `dog`, for example it might return `cat dog cat`.

### Textual Inversion Embeddings

- You can use `<embed:filename>` to use a Textual Inversion embedding anywhere.

### LoRAs

- You may use `<lora:filename:weight>` to enable a LoRA
    - Note that it's generally preferred to use the GUI at the bottom of the page to select loras
    - Note that usually position within the prompt doesn't matter, loras are not actually a prompt feature, this is just a convenience option for users used to Auto WebUI.
    - The one time it does matter, is when you use `<segment:...>` or `<object:...>`: a LoRA inside one of these will apply *only* to that segment or object.

### Presets

- You can use `<preset:presetname>` to inject a preset.
    - GUI is generally preferred for LoRAs, this is available to allow dynamically messing with presets (eg `<preset:<random:a, b>>`)

### Automatic Segmentation and Refining

- You can use `<segment:texthere>` to automatically refine part of the image using CLIP Segmentation.
    - This is like a "restore faces" feature but much more versatile, you can refine anything and control what it does.
    - Or `<segment:texthere,creativity,threshold>` - where creativity is inpaint strength, and threshold is segmentation minimum threshold - for example, `<segment:face,0.8,0.5>` - defaults to 0.6 creativity, 0.5 threshold.
    - See [the feature announcement](https://github.com/Stability-AI/StableSwarmUI/discussions/11#discussioncomment-7236821) for details.
    - Note the first time you run with CLIPSeg, Swarm will automatically download [an fp16 safetensors version of the clipseg-rd64-refined model](https://huggingface.co/mcmonkey/clipseg-rd64-refined-fp16)
    - You can insert a `<lora:...>` inside the prompt area of the segment to have a lora model apply onto that segment
    - You can also replace the `texthere` with `yolo-modelnamehere` to use YOLOv8 segmentation models
        - store your models in `(Swarm)/Models/yolov8`
        - Examples of valid YOLOv8 Segmentation models here: https://github.com/hben35096/assets/releases/
        - You can also do `yolo-modelnamehere-1` to grab exactly match #1, and `-2` for match #2, and etc.
            - You can do this all in one prompt to individual refine specific faces separately
            - Without this, if there are multiple people, it will do a bulk segmented refine on all faces combined
        - To control the creativity with a yolo model just append `,<creativity>,1`, for example `<segment:yolo-face_yolov8m-seg_60.pt-1,0.8,1>` sets a `0.8` creativity.
    - There's an advanced parameter under `Regional Prompting` named `Segment Model` to customize the base model used for segment processing
    - There's also a parameter named `Save Segment Mask` to save a preview copy of the generated mask

### Clear (Transparency)

- You can use `<clear:texthere>` to automatically clear parts of an image to transparent. This uses the same input format as `segment` (above) (for obvious reasons, this requires PNG not JPG).
    - For example, `<clear:background>` to clear the background.

### Break Keyword

- You can use `<break>` to specify a manual CLIP section break (eg in Auto WebUI this is `BREAK`).
    - If this is confusing, you this a bit of an internal hacky thing, so don't worry about. But if you want to know, here's the explanation:
        - CLIP (the model that processes text input to pass to SD), has a length of 75 _tokens_ (words basically).
        - By default, if you write a prompt that's longer than 75 tokens, what it will do is split 75/75, the first 75 tokens go in and become one CLIP result chunk, and then the next tokens get passed for a second CLIP chunk, and then the multiple CLIP results are parsed by SD in a batch and mixed as it goes.
        - The problem with this, is it's basically random - you might have eg `a photo of a big fluffy dog`, and it gets split into `a photo of a big fluffy` and then `dog` (in practice 75 tokens is a much longer prompt but just an example of how the split might go wrong)
        - Using `<break>` lets you manually specify where it splits, so you might do eg `a photo <break> big fluffy dog` (to intentionally put the style in one chunk and the subject in the next)
