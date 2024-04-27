import torch
import struct
from io import BytesIO
import latent_preview
import comfy
from server import PromptServer
from comfy.model_base import SDXL, SVD_img2vid
import numpy as np

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
    return torch.stack(noises, dim=0)

def swarm_send_extra_preview(id, image):
    server = PromptServer.instance
    bytesIO = BytesIO()
    num_data = 1 + (id * 16)
    header = struct.pack(">I", num_data)
    bytesIO.write(header)
    image.save(bytesIO, format="JPEG", quality=95, compress_level=4)
    preview_bytes = bytesIO.getvalue()
    server.send_sync(1, preview_bytes, sid=server.client_id)

def swarm_send_animated_preview(id, images):
    server = PromptServer.instance
    bytesIO = BytesIO()
    num_data = 3 + (id * 16)
    header = struct.pack(">I", num_data)
    bytesIO.write(header)
    images[0].save(bytesIO, save_all=True, duration=int(1000.0/6), append_images=images[1 : len(images)], lossless=False, quality=50, method=0, format='WEBP')
    bytesIO.seek(0)
    preview_bytes = bytesIO.getvalue()
    server.send_sync(1, preview_bytes, sid=server.client_id)

def calculate_sigmas_scheduler(model, scheduler_name, steps, sigma_min, sigma_max, rho):
    model_sampling = model.get_model_object("model_sampling")
    if scheduler_name == "karras":
        return comfy.k_diffusion.sampling.get_sigmas_karras(n=steps, sigma_min=sigma_min if sigma_min >= 0 else float(model_sampling.sigma_min), sigma_max=sigma_max if sigma_max >= 0 else float(model_sampling.sigma_max), rho=rho)
    elif scheduler_name == "exponential":
        return comfy.k_diffusion.sampling.get_sigmas_exponential(n=steps, sigma_min=sigma_min if sigma_min >= 0 else float(model_sampling.sigma_min), sigma_max=sigma_max if sigma_max >= 0 else float(model_sampling.sigma_max))
    else:
        return None

def make_swarm_sampler_callback(steps, device, model, previews):
    previewer = latent_preview.get_previewer(device, model.model.latent_format) if previews != "none" else None
    pbar = comfy.utils.ProgressBar(steps)
    def callback(step, x0, x, total_steps):
        pbar.update_absolute(step + 1, total_steps, None)
        if previewer:
            def do_preview(id, index):
                preview_img = previewer.decode_latent_to_preview_image("JPEG", x0[index:index+1])
                swarm_send_extra_preview(id, preview_img[1])
            if previews == "iterate":
                do_preview(0, step % x0.shape[0])
            elif previews == "animate":
                images = []
                for i in range(x0.shape[0]):
                    preview_img = previewer.decode_latent_to_preview_image("JPEG", x0[i:i+1])
                    images.append(preview_img[1])
                swarm_send_animated_preview(0, images)
            elif previews == "default":
                for i in range(x0.shape[0]):
                    preview_img = previewer.decode_latent_to_preview_image("JPEG", x0[i:i+1])
                    swarm_send_extra_preview(i, preview_img[1])
            elif previews == "one":
                do_preview(0, 0)
    return callback


def loglinear_interp(t_steps, num_steps):
    """
    Performs log-linear interpolation of a given array of decreasing numbers.
    """
    xs = np.linspace(0, 1, len(t_steps))
    ys = np.log(t_steps[::-1])

    new_xs = np.linspace(0, 1, num_steps)
    new_ys = np.interp(new_xs, xs, ys)

    interped_ys = np.exp(new_ys)[::-1].copy()
    return interped_ys

AYS_NOISE_LEVELS = {
    "SD1": [14.6146412293, 6.4745760956,  3.8636745985,  2.6946151520, 1.8841921177,  1.3943805092,  0.9642583904,  0.6523686016, 0.3977456272,  0.1515232662,  0.0291671582],
    "SDXL":[14.6146412293, 6.3184485287,  3.7681790315,  2.1811480769, 1.3405244945,  0.8620721141,  0.5550693289,  0.3798540708, 0.2332364134,  0.1114188177,  0.0291671582],
    "SVD": [700.00, 54.5, 15.886, 7.977, 4.248, 1.789, 0.981, 0.403, 0.173, 0.034, 0.002]
}


class SwarmKSampler:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "model": ("MODEL",),
                "noise_seed": ("INT", {"default": 0, "min": 0, "max": 0xffffffffffffffff}),
                "steps": ("INT", {"default": 20, "min": 1, "max": 10000}),
                "cfg": ("FLOAT", {"default": 8.0, "min": 0.0, "max": 100.0, "step": 0.5, "round": 0.001}),
                "sampler_name": (comfy.samplers.KSampler.SAMPLERS, ),
                "scheduler": (["turbo", "align_your_steps"] + comfy.samplers.KSampler.SCHEDULERS, ),
                "positive": ("CONDITIONING", ),
                "negative": ("CONDITIONING", ),
                "latent_image": ("LATENT", ),
                "start_at_step": ("INT", {"default": 0, "min": 0, "max": 10000}),
                "end_at_step": ("INT", {"default": 10000, "min": 0, "max": 10000}),
                "var_seed": ("INT", {"default": 0, "min": 0, "max": 0xffffffffffffffff}),
                "var_seed_strength": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 1.0, "step": 0.05, "round": 0.001}),
                "sigma_max": ("FLOAT", {"default": -1, "min": -1.0, "max": 1000.0, "step":0.01, "round": False}),
                "sigma_min": ("FLOAT", {"default": -1, "min": -1.0, "max": 1000.0, "step":0.01, "round": False}),
                "rho": ("FLOAT", {"default": 7.0, "min": 0.0, "max": 100.0, "step":0.01, "round": False}),
                "add_noise": (["enable", "disable"], ),
                "return_with_leftover_noise": (["disable", "enable"], ),
                "previews": (["default", "none", "one", "iterate", "animate"], )
            }
        }

    CATEGORY = "StableSwarmUI/sampling"
    RETURN_TYPES = ("LATENT",)
    FUNCTION = "sample"

    def sample(self, model, noise_seed, steps, cfg, sampler_name, scheduler, positive, negative, latent_image, start_at_step, end_at_step, var_seed, var_seed_strength, sigma_max, sigma_min, rho, add_noise, return_with_leftover_noise, previews):
        device = comfy.model_management.get_torch_device()
        latent_samples = latent_image["samples"]
        disable_noise = add_noise == "disable"

        if disable_noise:
            noise = torch.zeros(latent_samples.size(), dtype=latent_samples.dtype, layout=latent_samples.layout, device="cpu")
        else:
            noise = swarm_fixed_noise(noise_seed, latent_samples, var_seed, var_seed_strength)

        noise_mask = None
        if "noise_mask" in latent_image:
            noise_mask = latent_image["noise_mask"]

        sigmas = None
        if sigma_min >= 0 and sigma_max >= 0 and scheduler in ["karras", "exponential", "turbo", "align_your_steps"]:
            real_model, _, _, _, _ = comfy.sample.prepare_sampling(model, noise.shape, positive, negative, noise_mask)
            if scheduler == "turbo":
                timesteps = torch.flip(torch.arange(1, 11) * 100 - 1, (0,))[:steps]
                sigmas = model.model.model_sampling.sigma(timesteps)
                sigmas = torch.cat([sigmas, sigmas.new_zeros([1])])
            elif scheduler == "align_your_steps":
                if isinstance(model.model, SDXL):
                    model_type = "SDXL"
                elif isinstance(model.model, SVD_img2vid):
                    model_type = "SVD"
                else:
                    model_type = "SD1"
                sigmas = AYS_NOISE_LEVELS[model_type][:]
                if (steps + 1) != len(sigmas):
                    sigmas = loglinear_interp(sigmas, steps + 1)
                sigmas[-1] = 0
                sigmas = torch.FloatTensor(sigmas)
            elif sampler_name in ['dpm_2', 'dpm_2_ancestral']:
                sigmas = calculate_sigmas_scheduler(real_model, scheduler, steps + 1, sigma_min, sigma_max, rho)
                sigmas = torch.cat([sigmas[:-2], sigmas[-1:]])
            else:
                sigmas = calculate_sigmas_scheduler(real_model, scheduler, steps, sigma_min, sigma_max, rho)
            sigmas = sigmas.to(device)
        
        callback = make_swarm_sampler_callback(steps, device, model, previews)

        samples = comfy.sample.sample(model, noise, steps, cfg, sampler_name, scheduler, positive, negative, latent_samples,
                                    denoise=1.0, disable_noise=disable_noise, start_step=start_at_step, last_step=end_at_step,
                                    force_full_denoise=return_with_leftover_noise == "disable", noise_mask=noise_mask, sigmas=sigmas, callback=callback, seed=noise_seed)
        out = latent_image.copy()
        out["samples"] = samples
        return (out, )


NODE_CLASS_MAPPINGS = {
    "SwarmKSampler": SwarmKSampler,
}
