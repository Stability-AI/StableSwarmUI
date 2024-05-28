import torch

class SwarmSquareMaskFromPercent:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "x": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 1.0}),
                "y": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 1.0}),
                "width": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 1.0}),
                "height": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 1.0}),
                "strength": ("FLOAT", {"default": 1.0, "min": 0.0, "max": 1.0})
            }
        }

    CATEGORY = "StableSwarmUI/masks"
    RETURN_TYPES = ("MASK",)
    FUNCTION = "mask_from_perc"

    def mask_from_perc(self, x, y, width, height, strength):
        SCALE = 256
        mask = torch.zeros((SCALE, SCALE), dtype=torch.float32, device="cpu")
        mask[int(y*SCALE):int((y+height)*SCALE), int(x*SCALE):int((x+width)*SCALE)] = strength
        return (mask,)


def mask_size_match(mask_a, mask_b):
    if len(mask_a.shape) == 2:
        mask_a = mask_a.unsqueeze(0)
    if len(mask_b.shape) == 2:
        mask_b = mask_b.unsqueeze(0)
    height = max(mask_a.shape[1], mask_b.shape[1])
    width = max(mask_a.shape[2], mask_b.shape[2])
    if mask_a.shape[1] != height or mask_a.shape[2] != width:
        mask_a = torch.nn.functional.interpolate(mask_a.unsqueeze(0), size=(height, width), mode="bicubic")[0]
    if mask_b.shape[1] != height or mask_b.shape[2] != width:
        mask_b = torch.nn.functional.interpolate(mask_b.unsqueeze(0), size=(height, width), mode="bicubic")[0]
    return (mask_a, mask_b)


class SwarmOverMergeMasksForOverlapFix:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "mask_a": ("MASK",),
                "mask_b": ("MASK",),
            }
        }

    CATEGORY = "StableSwarmUI/masks"
    RETURN_TYPES = ("MASK",)
    FUNCTION = "mask_overmerge"

    def mask_overmerge(self, mask_a, mask_b):
        mask_a, mask_b = mask_size_match(mask_a, mask_b)
        mask_sum = mask_a + mask_b
        return (mask_sum,)


class SwarmCleanOverlapMasks:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "mask_a": ("MASK",),
                "mask_b": ("MASK",),
            }
        }

    CATEGORY = "StableSwarmUI/masks"
    RETURN_TYPES = ("MASK","MASK",)
    FUNCTION = "mask_overlap"

    def mask_overlap(self, mask_a, mask_b):
        mask_a, mask_b = mask_size_match(mask_a, mask_b)
        mask_sum = mask_a + mask_b
        mask_sum = mask_sum.clamp(1.0, 9999.0)
        mask_a = mask_a / mask_sum
        mask_b = mask_b / mask_sum
        return (mask_a, mask_b)


class SwarmCleanOverlapMasksExceptSelf:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "mask_self": ("MASK",),
                "mask_merged": ("MASK",),
            }
        }

    CATEGORY = "StableSwarmUI/masks"
    RETURN_TYPES = ("MASK",)
    FUNCTION = "mask_clean"

    def mask_clean(self, mask_self, mask_merged):
        mask_self, mask_merged = mask_size_match(mask_self, mask_merged)
        mask_sum = mask_merged.clamp(1.0, 9999.0)
        mask_self = mask_self / mask_sum
        return (mask_self,)


class SwarmExcludeFromMask:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "main_mask": ("MASK",),
                "exclude_mask": ("MASK",),
            }
        }

    CATEGORY = "StableSwarmUI/masks"
    RETURN_TYPES = ("MASK",)
    FUNCTION = "mask_exclude"

    def mask_exclude(self, main_mask, exclude_mask):
        main_mask, exclude_mask = mask_size_match(main_mask, exclude_mask)
        main_mask = main_mask - exclude_mask
        main_mask = main_mask.clamp(0.0, 1.0)
        return (main_mask,)


class SwarmMaskBounds:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "mask": ("MASK",),
                "grow": ("INT", {"default": 0, "min": 0, "max": 1024})
            }
        }

    CATEGORY = "StableSwarmUI/masks"
    RETURN_TYPES = ("INT", "INT", "INT", "INT")
    RETURN_NAMES = ("x", "y", "width", "height")
    FUNCTION = "get_bounds"

    def get_bounds(self, mask, grow):
        if len(mask.shape) == 3:
            mask = mask[0]
        sum_x = (torch.sum(mask, dim=0) != 0).to(dtype=torch.int)
        sum_y = (torch.sum(mask, dim=1) != 0).to(dtype=torch.int)
        def getval(arr, direction):
            val = torch.argmax(arr).item()
            val += grow * direction
            val = max(0, min(val, arr.shape[0] - 1))
            return val
        x_start = getval(sum_x, -1)
        x_end = mask.shape[1] - getval(sum_x.flip(0), -1)
        y_start = getval(sum_y, -1)
        y_end = mask.shape[0] - getval(sum_y.flip(0), -1)
        return (int(x_start), int(y_start), int(x_end - x_start), int(y_end - y_start))


# Blur code is copied out of ComfyUI's default ImageBlur
def gaussian_kernel(kernel_size: int, sigma: float, device=None):
    x, y = torch.meshgrid(torch.linspace(-1, 1, kernel_size, device=device), torch.linspace(-1, 1, kernel_size, device=device), indexing="ij")
    d = torch.sqrt(x * x + y * y)
    g = torch.exp(-(d * d) / (2.0 * sigma * sigma))
    return g / g.sum()


class SwarmMaskBlur:
    def __init__(self):
        pass

    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "mask": ("MASK",),
                "blur_radius": ("INT", { "default": 1, "min": 1, "max": 64, "step": 1 }),
                "sigma": ("FLOAT", { "default": 1.0, "min": 0.1, "max": 10.0, "step": 0.1 }),
            },
        }

    RETURN_TYPES = ("MASK",)
    FUNCTION = "blur"

    CATEGORY = "StableSwarmUI/masks"

    def blur(self, mask, blur_radius, sigma):
        if blur_radius == 0:
            return (mask,)
        kernel_size = blur_radius * 2 + 1
        kernel = gaussian_kernel(kernel_size, sigma, device=mask.device).repeat(1, 1, 1).unsqueeze(1)
        while mask.ndim < 4:
            mask = mask.unsqueeze(0)
        padded_mask = torch.nn.functional.pad(mask, (blur_radius,blur_radius,blur_radius,blur_radius), 'reflect')
        blurred = torch.nn.functional.conv2d(padded_mask, kernel, padding=kernel_size // 2, groups=1)[:,:,blur_radius:-blur_radius, blur_radius:-blur_radius]
        blurred = blurred.squeeze(0).squeeze(0)
        return (blurred,)


class SwarmMaskThreshold:
    def __init__(self):
        pass

    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "mask": ("MASK",),
                "min": ("FLOAT", { "default": 0.2, "min": 0, "max": 1, "step": 0.01 }),
                "max": ("FLOAT", { "default": 0.8, "min": 0, "max": 1, "step": 0.01 }),
            },
        }

    RETURN_TYPES = ("MASK",)
    FUNCTION = "threshold"
    CATEGORY = "StableSwarmUI/masks"

    def threshold(self, mask, min, max):
        mask = mask.clamp(min, max)
        mask = mask - min
        mask = mask / (max - min)
        return (mask,)


NODE_CLASS_MAPPINGS = {
    "SwarmSquareMaskFromPercent": SwarmSquareMaskFromPercent,
    "SwarmCleanOverlapMasks": SwarmCleanOverlapMasks,
    "SwarmCleanOverlapMasksExceptSelf": SwarmCleanOverlapMasksExceptSelf,
    "SwarmExcludeFromMask": SwarmExcludeFromMask,
    "SwarmOverMergeMasksForOverlapFix": SwarmOverMergeMasksForOverlapFix,
    "SwarmMaskBounds": SwarmMaskBounds,
    "SwarmMaskBlur": SwarmMaskBlur,
    "SwarmMaskThreshold": SwarmMaskThreshold,
}
