import torch

class SwarmLatentBlendMasked:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "samples1": ("LATENT",),
                "samples2": ("LATENT",),
                "mask": ("MASK",),
                "blend_factor": ("FLOAT", { "default": 0.5, "min": 0, "max": 1, "step": 0.01 }),
            }
        }

    RETURN_TYPES = ("LATENT",)
    FUNCTION = "blend"

    CATEGORY = "StableSwarmUI"

    def blend(self, samples1, samples2, blend_factor, mask):
        samples_out = samples1.copy()
        samples1 = samples1["samples"]
        samples2 = samples2["samples"]
        if len(mask.shape) == 2:
            mask = mask.unsqueeze(0)
        mask = mask.unsqueeze(0)

        if samples1.shape != samples2.shape:
            samples2 = torch.nn.functional.interpolate(samples2, size=(samples1.shape[3], samples1.shape[2]), mode="bicubic")
        if samples1.shape != mask.shape:
            mask = torch.nn.functional.interpolate(mask, size=(samples1.shape[3], samples1.shape[2]), mode="bicubic")

        mask = 1 - mask
        mask_pos = 1 - (mask * blend_factor)
        mask_neg = 1 - (mask * (1 - blend_factor))

        samples_blended = samples1 * mask_pos + samples2 * mask_neg
        samples_out["samples"] = samples_blended
        return (samples_out,)


NODE_CLASS_MAPPINGS = {
    "SwarmLatentBlendMasked": SwarmLatentBlendMasked,
}
