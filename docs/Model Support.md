# Model Type Support In StableSwarmUI

Swarm natively supports [ModelSpec](https://github.com/Stability-AI/ModelSpec) metadata and can import metadata from some legacy formats used by other UIs (auto webui thumbnails, matrix jsons, etc)

Swarm supports models of all the common architectures:

### Stable Diffusion v1 and v2

SDv1/SDv2 models work exactly as normal. Even legacy (pre-[ModelSpec](https://github.com/Stability-AI/ModelSpec) models are supported).

### Stable Diffusion v1 Inpainting Models

SDv1 inpaint models (RunwayML) are supported, but will work best if you manually edit the Architecture ID to be `stable-diffusion-v1/inpaint`.

### Stable Diffusion XL

SDXL models work as normal, with the bonus that by default enhanced inference settings will be used (eg scaled up rescond).

Additional, SDXL-Refiner architecture models can be inferenced, both as refiner or even as a base (you must manually set res to 512x512 and it will generate weird results).

### Stable Diffusion 3

Stable Diffusion 3 will be supported when it's released.

### SDXL Turbo and SD Turbo

Turbo models work the same as regular models, just set `CFG Scale` to `1` and `Steps` to `1` as well. Under the `Sampling` group set `Scheduler` to `Turbo`.

### Latency Consistency Models

LCM models work the same as regular models, just set `CFG Scale` to `1` and `Steps` to `4`. Under the `Sampling` group set `Sampler` to `lcm`.

### Lightning Models

Lightning models work the same as regular models, just set `CFG Scale` to `1` and (TODO: Sampling specifics for lightning).

### SegMind SSD-1B

SegMind SSD-1B models work the same as SD models.

### Stable Video Diffusion

SVD models are supported via the "Video" parameter group. Like XL, video by default uses enhanced inference settings (better sampler and larger sigma value).

You can do text2video by just checking Video as normal, or image2video by using an Init Image and setting Creativity to 0.

### Stable Cascade

Stable Cascade is supported if you use the "ComfyUI Format" models (aka "All In One") https://huggingface.co/stabilityai/stable-cascade/tree/main/comfyui_checkpoints that come as a pair of `stage_b` and `stage_c` models.

You must keep the two in the same folder, named the same with the only difference being `stage_b` vs `stage_c` in the filename.

Either model can be selected in the UI to use them, it will automatically use both.

### TensorRT

TensorRT support (`.engine`) is available for SDv1, SDv2-768-v, SDXL Base, SDXL Refiner

You can generate TensorRT engines from the model menu
