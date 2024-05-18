import torch

class SwarmLatentBlendMasked:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "samples0": ("LATENT",),
                "samples1": ("LATENT",),
                "mask": ("MASK",),
                "blend_factor": ("FLOAT", { "default": 0.5, "min": 0, "max": 1, "step": 0.01 }),
            }
        }

    RETURN_TYPES = ("LATENT",)
    FUNCTION = "blend"

    CATEGORY = "StableSwarmUI/images"

    def blend(self, samples0, samples1, blend_factor, mask):
        samples_out = samples0.copy()
        samples0 = samples0["samples"]
        samples1 = samples1["samples"]
        while mask.ndim < 4:
            mask = mask.unsqueeze(0)

        if samples0.shape != samples1.shape:
            samples1 = torch.nn.functional.interpolate(samples1, size=(samples0.shape[2], samples0.shape[3]), mode="bicubic")
        if samples0.shape != mask.shape:
            mask = torch.nn.functional.interpolate(mask, size=(samples0.shape[2], samples0.shape[3]), mode="bicubic")

        samples_blended = samples0 * (1 - mask * blend_factor) + samples1 * (mask * blend_factor)
        samples_out["samples"] = samples_blended
        return (samples_out,)


NODE_CLASS_MAPPINGS = {
    "SwarmLatentBlendMasked": SwarmLatentBlendMasked,
}
