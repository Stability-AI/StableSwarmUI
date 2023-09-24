import torch
import struct
from io import BytesIO
import latent_preview
import comfy
from server import PromptServer

def slerp(val, low, high):
    low_norm = low / torch.norm(low, dim=1, keepdim=True)
    high_norm = high / torch.norm(high, dim=1, keepdim=True)
    dot = (low_norm * high_norm).sum(1)
    if dot.mean() > 0.9995:
        return low * val + high * (1 - val)
    omega = torch.acos(dot)
    so = torch.sin(omega)
    res = (torch.sin((1.0 - val) * omega) / so).unsqueeze(1) * low + (torch.sin(val * omega) / so).unsqueeze(1) * high
    return res

def swarm_partial_noise(seed, latent_image):
    generator = torch.manual_seed(seed)
    return torch.randn(latent_image.size(), dtype=latent_image.dtype, layout=latent_image.layout, generator=generator, device="cpu")

def swarm_fixed_noise(seed, latent_image, var_seed, var_seed_strength):
    noises = []
    for i in range(latent_image.size()[0]):
        if var_seed_strength > 0:
            noise = swarm_partial_noise(seed, latent_image[i])
            var_noise = swarm_partial_noise(var_seed + i, latent_image[i])
            noise = slerp(var_seed_strength, noise, var_noise)
        else:
            noise = swarm_partial_noise(seed + i, latent_image[i])
        noises.append(noise)
    return torch.stack(noises, axis=0)

def swarm_send_extra_preview(id, image):
    server = PromptServer.instance
    bytesIO = BytesIO()
    num_data = 1 + (id * 16)
    header = struct.pack(">I", num_data)
    bytesIO.write(header)
    image.save(bytesIO, format="JPEG", quality=95, compress_level=4)
    preview_bytes = bytesIO.getvalue()
    server.send_sync(1, preview_bytes, sid=server.client_id)

class SwarmKSampler:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "model": ("MODEL",),
                "noise_seed": ("INT", {"default": 0, "min": 0, "max": 0xffffffffffffffff}),
                "steps": ("INT", {"default": 20, "min": 1, "max": 10000}),
                "cfg": ("FLOAT", {"default": 8.0, "min": 0.0, "max": 100.0, "step":0.5, "round": 0.01}),
                "sampler_name": (comfy.samplers.KSampler.SAMPLERS, ),
                "scheduler": (comfy.samplers.KSampler.SCHEDULERS, ),
                "positive": ("CONDITIONING", ),
                "negative": ("CONDITIONING", ),
                "latent_image": ("LATENT", ),
                "start_at_step": ("INT", {"default": 0, "min": 0, "max": 10000}),
                "end_at_step": ("INT", {"default": 10000, "min": 0, "max": 10000}),
                "var_seed": ("INT", {"default": 0, "min": 0, "max": 0xffffffffffffffff}),
                "var_seed_strength": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 1.0, "step":0.01})
            }
        }

    CATEGORY = "StableSwarmUI"
    RETURN_TYPES = ("LATENT",)
    FUNCTION = "sample"

    def sample(self, model, noise_seed, steps, cfg, sampler_name, scheduler, positive, negative, latent_image, start_at_step, end_at_step, var_seed, var_seed_strength):
        device = comfy.model_management.get_torch_device()
        latent_samples = latent_image["samples"]

        noise = swarm_fixed_noise(noise_seed, latent_samples, var_seed, var_seed_strength)

        noise_mask = None
        if "noise_mask" in latent_image:
            noise_mask = latent_image["noise_mask"]

        previewer = latent_preview.get_previewer(device, model.model.latent_format)

        pbar = comfy.utils.ProgressBar(steps)
        def callback(step, x0, x, total_steps):
            pbar.update_absolute(step + 1, total_steps, None)
            if previewer:
                for i in range(x0.shape[0]):
                    preview_img = previewer.decode_latent_to_preview_image("JPEG", x0[i:i+1])
                    swarm_send_extra_preview(i, preview_img[1])

        samples = comfy.sample.sample(model, noise, steps, cfg, sampler_name, scheduler, positive, negative, latent_samples,
                                    denoise=1.0, disable_noise=False, start_step=start_at_step, last_step=end_at_step,
                                    force_full_denoise=False, noise_mask=noise_mask, callback=callback, seed=noise_seed)
        out = latent_image.copy()
        out["samples"] = samples
        return (out, )


NODE_CLASS_MAPPINGS = {
    "SwarmKSampler": SwarmKSampler,
}
