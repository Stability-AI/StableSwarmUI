import torch

class SwarmSquareMaskFromPercent:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "x": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 1.0}),
                "y": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 1.0}),
                "width": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 1.0}),
                "height": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 1.0})
            }
        }

    CATEGORY = "StableSwarmUI"
    RETURN_TYPES = ("MASK",)
    FUNCTION = "mask_from_perc"

    def mask_from_perc(self, x, y, width, height):
        SCALE = 256
        mask = torch.zeros((SCALE, SCALE), dtype=torch.float32, device="cpu")
        mask[int(y*SCALE):int((y+height)*SCALE), int(x*SCALE):int((x+width)*SCALE)] = 1.0
        return (mask,)


def mask_size_match(mask_a, mask_b):
    height = max(mask_a.shape[0], mask_b.shape[0])
    width = max(mask_a.shape[1], mask_b.shape[1])
    if mask_a.shape[0] != height or mask_a.shape[1] != width:
        mask_a = torch.nn.functional.interpolate(mask_a, size=(height, width), mode="linear")
    if mask_b.shape[0] != height or mask_b.shape[1] != width:
        mask_b = torch.nn.functional.interpolate(mask_b, size=(height, width), mode="linear")
    return (mask_a, mask_b)


class SwarmCleanOverlapMasks:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "mask_a": ("MASK",),
                "mask_b": ("MASK",),
            }
        }

    CATEGORY = "StableSwarmUI"
    RETURN_TYPES = ("MASK","MASK",)
    FUNCTION = "mask_overlap"

    def mask_overlap(self, mask_a, mask_b):
        mask_a, mask_b = mask_size_match(mask_a, mask_b)
        mask_sum = mask_a + mask_b
        mask_sum = mask_sum.clamp(1.0, 2.0)
        mask_a = mask_a / mask_sum
        mask_b = mask_b / mask_sum
        return (mask_a, mask_b)


class SwarmExcludeFromMask:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "main_mask": ("MASK",),
                "exclude_mask": ("MASK",),
            }
        }

    CATEGORY = "StableSwarmUI"
    RETURN_TYPES = ("MASK",)
    FUNCTION = "mask_overlap"

    def mask_overlap(self, main_mask, exclude_mask):
        main_mask, exclude_mask = mask_size_match(main_mask, exclude_mask)
        main_mask = main_mask - exclude_mask
        main_mask = main_mask.clamp(0.0, 1.0)
        return (main_mask,)


NODE_CLASS_MAPPINGS = {
    "SwarmSquareMaskFromPercent": SwarmSquareMaskFromPercent,
    "SwarmCleanOverlapMasks": SwarmCleanOverlapMasks,
    "SwarmExcludeFromMask": SwarmExcludeFromMask,
}
