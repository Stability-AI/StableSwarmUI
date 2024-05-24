import torch, comfy
from .SwarmKSampler import make_swarm_sampler_callback

class SwarmUnsampler:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "model": ("MODEL",),
                "steps": ("INT", {"default": 20, "min": 1, "max": 10000}),
                "sampler_name": (comfy.samplers.KSampler.SAMPLERS, ),
                "scheduler": (["turbo"] + comfy.samplers.KSampler.SCHEDULERS, ),
                "positive": ("CONDITIONING", ),
                "negative": ("CONDITIONING", ),
                "latent_image": ("LATENT", ),
                "start_at_step": ("INT", {"default": 0, "min": 0, "max": 10000}),
                "previews": (["default", "none", "one"], )
            }
        }

    CATEGORY = "StableSwarmUI/sampling"
    RETURN_TYPES = ("LATENT",)
    FUNCTION = "unsample"

    def unsample(self, model, steps, sampler_name, scheduler, positive, negative, latent_image, start_at_step, previews):
        device = comfy.model_management.get_torch_device()
        latent_samples = latent_image["samples"].to(device)

        noise = torch.zeros(latent_samples.size(), dtype=latent_samples.dtype, layout=latent_samples.layout, device=device)
        noise_mask = None
        if "noise_mask" in latent_image:
            noise_mask = latent_image["noise_mask"]

        sampler = comfy.samplers.KSampler(model, steps=steps, device=device, sampler=sampler_name, scheduler=scheduler, denoise=1.0, model_options=model.model_options)
        sigmas = sampler.sigmas.flip(0) + 0.0001

        callback = make_swarm_sampler_callback(steps, device, model, previews)

        samples = comfy.sample.sample(model, noise, steps, 1, sampler_name, scheduler, positive, negative, latent_samples,
                                    denoise=1.0, disable_noise=False, start_step=0, last_step=steps - start_at_step,
                                    force_full_denoise=False, noise_mask=noise_mask, sigmas=sigmas, callback=callback, seed=0)
        out = latent_image.copy()
        out["samples"] = samples
        return (out, )


NODE_CLASS_MAPPINGS = {
    "SwarmUnsampler": SwarmUnsampler,
}
