import torch, copy

def make_circular(m):
    if isinstance(m, torch.nn.Conv2d):
        m.padding_mode = "circular"

class SwarmModelTiling:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "model": ("MODEL", ),
            }
        }

    CATEGORY = "StableSwarmUI"
    RETURN_TYPES = ("MODEL",)
    FUNCTION = "adapt"

    def adapt(self, model):
        m = model.clone()
        m.model.apply(make_circular)
        return (m,)

class SwarmTileableVAEDecode:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "samples": ("LATENT", ),
                "vae": ("VAE", )
            }
        }

    CATEGORY = "StableSwarmUI"
    RETURN_TYPES = ("IMAGE",)
    FUNCTION = "decode"

    def decode(self, vae, samples):
        vae = copy.deepcopy(vae)
        vae.first_stage_model.apply(make_circular)
        decoded = vae.decode(samples["samples"])
        return (decoded,)

NODE_CLASS_MAPPINGS = {
    "SwarmModelTiling": SwarmModelTiling,
    "SwarmTileableVAEDecode": SwarmTileableVAEDecode,
}
