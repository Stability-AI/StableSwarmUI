import torch, copy
from torch.nn import functional as F

def make_circular_assym(m, assym_mode):
    def _conv_forward(self, input, weight, bias):
        if self.padding_mode == "x_circular":
            padded = F.pad(input, (self._reversed_padding_repeated_twice[0], self._reversed_padding_repeated_twice[1], 0, 0), "circular")
            padded = F.pad(padded, (0, 0, self._reversed_padding_repeated_twice[2], self._reversed_padding_repeated_twice[3]), "constant", 0)
            return F.conv2d(padded, weight, bias, self.stride, (0, 0), self.dilation, self.groups)
        elif self.padding_mode == "y_circular":
            padded = F.pad(input, (self._reversed_padding_repeated_twice[0], self._reversed_padding_repeated_twice[1], 0, 0), "constant", 0)
            padded = F.pad(padded, (0, 0, self._reversed_padding_repeated_twice[2], self._reversed_padding_repeated_twice[3]), "circular")
            return F.conv2d(padded, weight, bias, self.stride, (0, 0), self.dilation, self.groups)
        elif self.padding_mode != "zeros":
            padded = F.pad(input, self._reversed_padding_repeated_twice, mode=self.padding_mode)
            return F.conv2d(padded, weight, bias, self.stride, (0, 0), self.dilation, self.groups)
        else:
            return F.conv2d(input, weight, bias, self.stride, self.padding, self.dilation, self.groups)
    if isinstance(m, torch.nn.Conv2d):
        m._conv_forward = _conv_forward.__get__(m, torch.nn.Conv2d)
        m.padding_mode = assym_mode

def make_circular(m):
    if isinstance(m, torch.nn.Conv2d):
        m.padding_mode = "circular"

class SwarmModelTiling:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "model": ("MODEL", ),
            },
            "optional": {
                "tile_axis": (["Both", "X", "Y"], )
            }
        }

    CATEGORY = "StableSwarmUI/sampling"
    RETURN_TYPES = ("MODEL",)
    FUNCTION = "adapt"

    def adapt(self, model, tile_axis=None):
        m = copy.deepcopy(model)
        if tile_axis is not None and tile_axis != "Both":
            if tile_axis == "X":
                m.model.apply(lambda x: make_circular_assym(x, "x_circular"))
            elif tile_axis == "Y":
                m.model.apply(lambda x: make_circular_assym(x, "y_circular"))
        else:
            m.model.apply(make_circular)
        return (m,)

class SwarmTileableVAE:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "vae": ("VAE", )
            },
            "optional": {
                "tile_axis": (["Both", "X", "Y"], )
            }
        }

    CATEGORY = "StableSwarmUI/sampling"
    RETURN_TYPES = ("VAE",)
    FUNCTION = "adapt"

    def adapt(self, vae, tile_axis=None):
        vae = copy.deepcopy(vae)
        if tile_axis is not None and tile_axis != "Both":
            if tile_axis == "X":
                vae.first_stage_model.apply(lambda x: make_circular_assym(x, "x_circular"))
            elif tile_axis == "Y":
                vae.first_stage_model.apply(lambda x: make_circular_assym(x, "y_circular"))
        else:
            vae.first_stage_model.apply(make_circular)
        return (vae,)

NODE_CLASS_MAPPINGS = {
    "SwarmModelTiling": SwarmModelTiling,
    "SwarmTileableVAE": SwarmTileableVAE,
}
