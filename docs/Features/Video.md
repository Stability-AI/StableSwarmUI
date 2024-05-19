# Video Generation in StableSwarmUI

- Video generation in StableSwarmUI is powered by [Stable Video Diffusion](https://arxiv.org/abs/2311.15127)
- You can find the official SVD XT 1.1 model [here](https://huggingface.co/stabilityai/stable-video-diffusion-img2vid-xt-1-1) (requires a HuggingFace account to download)
    - Store the model in your Stable-Diffusion models folder
- To use Video, simply enable the Video parameter group
    - It should automatically select your Video model, but you can select one manually.
    - The default settings all work great, but customize at will.
        - Setting your Sampler to AlignYourSteps might be beneficial to use lower step counts with SVD.
    - A lot of extra parameters here are hidden beneath "Display Advanced Options"
    - By default, you'll get live animated previews (webp-animation). These incur a slight performance penalty but give you a very clear view of the video that is generating (vs traditional single-frame preview).
    - For the final video format, "webp" is most convenient for usage within Swarm, but can be awkward for sharing as not all programs support webp (eg Discord doesn't). h264-mp4 is easier to share, but may misbehave slightly in the UI.
    - Frame interpolation can make your videos a bit nicer - a repo from [Fannovel16](https://github.com/Fannovel16/ComfyUI-Frame-Interpolation) helps with this. The bottom of the video parameter listing has a button to automatically install it for you if you want to use frame interpolation.
        - When installed, simply set the "Frame Interpolation Multiplier" to a higher value (eg 2, to interpolate to twice as many frames as SVD generated), and pick between the Methods available (they are similar, differences are subjective.)
    - Got bad video results? Well... yeah. Unfortunately, AI video tech is still young, and there's a lot of trial-and-error, as well as a lot of luck-of-seeds.
        - Reducing the Motion Bucket can help reduce corruption. The AlignYourSteps sampler can also help. Naturally, higher Steps helps too (at the cost of taking much longer to generate).
