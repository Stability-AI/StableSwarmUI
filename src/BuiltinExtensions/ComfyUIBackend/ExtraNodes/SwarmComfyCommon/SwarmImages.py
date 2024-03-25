import torch
import comfy
import math

class SwarmImageScaleForMP:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "image": ("IMAGE",),
                "width": ("INT", {"default": 0, "min": 0, "max": 8192}),
                "height": ("INT", {"default": 0, "min": 0, "max": 8192}),
                "can_shrink": ("BOOLEAN", {"default": True}),
            }
        }

    CATEGORY = "StableSwarmUI/images"
    RETURN_TYPES = ("IMAGE",)
    FUNCTION = "scale"

    def scale(self, image, width, height, can_shrink):
        mpTarget = width * height
        oldWidth = image.shape[2]
        oldHeight = image.shape[1]

        scale = math.sqrt(mpTarget / (oldWidth * oldHeight))
        if not can_shrink and scale < 1:
            return (image,)
        newWid = int(round(oldWidth * scale / 64) * 64)
        newHei = int(round(oldHeight * scale / 64) * 64)
        samples = image.movedim(-1, 1)
        s = comfy.utils.common_upscale(samples, newWid, newHei, "bilinear", "disabled")
        s = s.movedim(1, -1)
        return (s,)

class SwarmImageCrop:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "image": ("IMAGE",),
                "x": ("INT", {"default": 0, "min": 0, "max": 8192, "step": 8}),
                "y": ("INT", {"default": 0, "min": 0, "max": 8192, "step": 8}),
                "width": ("INT", {"default": 512, "min": 64, "max": 8192, "step": 8}),
                "height": ("INT", {"default": 512, "min": 64, "max": 8192, "step": 8}),
            }
        }

    CATEGORY = "StableSwarmUI/images"
    RETURN_TYPES = ("IMAGE",)
    FUNCTION = "crop"

    def crop(self, image, x, y, width, height):
        if width <= 0 or height <= 0:
            return (image,)
        to_x = width + x
        to_y = height + y
        img = image[:, y:to_y, x:to_x, :]
        return (img,)

class SwarmVideoBoomerang:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "images": ("IMAGE",),
            }
        }

    CATEGORY = "StableSwarmUI/video"
    RETURN_TYPES = ("IMAGE",)
    FUNCTION = "boomerang"

    def boomerang(self, images):
        # return images followed by  reverse images
        images = torch.cat((images, images.flip(0)), 0)
        return (images,)

NODE_CLASS_MAPPINGS = {
    "SwarmImageScaleForMP": SwarmImageScaleForMP,
    "SwarmImageCrop": SwarmImageCrop,
    "SwarmVideoBoomerang": SwarmVideoBoomerang,
}
