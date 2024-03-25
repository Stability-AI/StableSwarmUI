import comfy, torch

class SwarmOffsetEmptyLatentImage:
    def __init__(self):
        self.device = comfy.model_management.intermediate_device()

    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "width": ("INT", {"default": 512, "min": 16, "max": 4096, "step": 8}),
                "height": ("INT", {"default": 512, "min": 16, "max": 4096, "step": 8}),
                "off_a": ("INT", {"default": 0, "min": -10, "max": 10, "step": 0.0001}),
                "off_b": ("INT", {"default": 0, "min": -10, "max": 10, "step": 0.0001}),
                "off_c": ("INT", {"default": 0, "min": -10, "max": 10, "step": 0.0001}),
                "off_d": ("INT", {"default": 0, "min": -10, "max": 10, "step": 0.0001}),
                "batch_size": ("INT", {"default": 1, "min": 1, "max": 4096})
            }
        }

    CATEGORY = "StableSwarmUI/latents"
    RETURN_TYPES = ("LATENT",)
    FUNCTION = "generate"

    def generate(self, width, height, off_a, off_b, off_c, off_d, batch_size=1):
        latent = torch.zeros([batch_size, 4, height // 8, width // 8], device=self.device)
        latent[:, 0, :, :] = off_a
        latent[:, 1, :, :] = off_b
        latent[:, 2, :, :] = off_c
        latent[:, 3, :, :] = off_d
        return ({"samples":latent}, )


NODE_CLASS_MAPPINGS = {
    "SwarmOffsetEmptyLatentImage": SwarmOffsetEmptyLatentImage
}
