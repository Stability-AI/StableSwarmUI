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

class SwarmTileableVAE:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "vae": ("VAE", )
            }
        }

    CATEGORY = "StableSwarmUI"
    RETURN_TYPES = ("VAE",)
    FUNCTION = "decode"

    def decode(self, vae):
        vae = copy.deepcopy(vae)
        vae.first_stage_model.apply(make_circular)
        return (vae,)

NODE_CLASS_MAPPINGS = {
    "SwarmModelTiling": SwarmModelTiling,
    "SwarmTileableVAE": SwarmTileableVAE,
}
