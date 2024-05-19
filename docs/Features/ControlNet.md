# ControlNets in StableSwarmUI

- ControlNets are a form of guidance for Stable Diffusion that take in a reference image, and copy some feature set from it
    - For example, "Canny" controlnets copy the linework for images. This is useful for image to easily turn a sketch into a photo, while keeping the same structure.
    - Or, "Depth" controlnets copy the 3D structure of an image. This is useful to convert between image content while keeping otherwise the same, such as replacing one person for another, or a cat for a dog, while keeping them in the same pose/situation/etc.
    - ControlNets are similar to but not the same as [IP-Adapter](/docs/Features/IPAdapter-ReVision.md), which copy more vague concepts from images, such as overall concepts in an image, or the facial structure of a person.
    - ControlNets were originally developed by [Lvmin Zhang](https://arxiv.org/abs/2302.05543), and have since been adopted by and used throughout the Stable Diffusion community.
    - ControlNet support in Swarm is provided natively by the ComfyUI backend, with help from [ControlNet Auxiliary Preprocessors developed by Fannovel16](https://github.com/Fannovel16/comfyui_controlnet_aux).
- ControlNets can be used through the "ControlNet" parameter grouping.
    - To get started, you'll need a model. There are official SDXL ControlNet LoRAs from Stability AI [here](https://huggingface.co/stabilityai/control-lora), and there's a general collection of community ControlNet models [here](https://huggingface.co/lllyasviel/sd_control_collection/tree/main) that you can use.
        - Unfortunately there's a bit too much variety of choice here so it can't be automated away. If you're unsure, the Stability AI official SDXL Canny and Depth models are a good starting set.
        - Save the models you want into `(Swarm)/Models/controlnet`
        - Afterwards, go to the "ControlNets" list on the bottom of the Generate tab and click the lil spinny icon (Refresh, not your browser refresh, the one on the model list).
    - To use any ControlNet other than Canny, you'll need to install Preprocessors - luckily, this one's automated. Just click the "Install Controlnet Preprocessors" button at the bottom of the ControlNet parameter group, and accept the confirmation prompt.
        - This will take a moment to download, install, and restart your backend. Check the server logs for a progress report. When it's done, the UI will automatically update.
    - To get started, just click a model you want in the ControlNets models list.
        - Then, open the ControlNet parameter group
        - Then drag an image to the "Choose File" slot - or leave it disabled and it will use the Init Image if you have one.
        - Some models (canny, depth) will autodetect the preprocessor to use. For others, you may have to manually select it.
        - Then generate an image - if all went well, you'll see an image guided by both your prompt, and the ControlNet.
    - You can check what the preprocessor does by hitting the "Preview" button. This will run the preprocessor and display the result. This is usually pretty quick.
    - You can check the "`Display Advanced`" box to get a few extra options:
        - In the param group, you'll have "ControlNet Start" and "End", to limit where the controlnet applies. Starting late prevents it from affecting overall image structure, and ending early prevents it from messing up the finer details.
        - You also get a "ControlNet Two" and "Three" group, if you want to do more ControlNets in a single generation call.

## Troubleshooting

- If you get an error while running with a ControlNet:
    - Do you get a message that looks kinda like "`ComfyUI execution error: mat1 and mat2 shapes cannot be multiplied (308x2048 and 768x320)`"?
        - If so, you probably have mixed up an SDv1 ControlNet with an SDXL model, or vice versa. Unfortunately, they don't mix: you need to use SDv1 Controlnets on SDv1 models, and SDXL controlnets on SDXL models.
    - Does your error happen when you use the "Preview" button, or only when generating?
        - If it happens only when generating, something's wrong with the model.
        - If it happens when you hit "Preview", something's wrong with the preprocessor or your input image.
    - Do you have the message `Cannot preview a ControlNet preprocessor without any preprocessor enabled.`?
        - You probably selected a model that can't be autodetected: just select a preprocessor from the list.
