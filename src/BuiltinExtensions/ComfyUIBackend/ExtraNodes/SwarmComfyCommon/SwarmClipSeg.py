import torch
from PIL import Image
import numpy as np
from transformers import CLIPSegProcessor, CLIPSegForImageSegmentation

class SwarmClipSeg:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "images": ("IMAGE",),
                "match_text": ("STRING", {"multiline": True}),
                "threshold": ("FLOAT", {"default": 0.5, "min": 0.0, "max": 1.0, "step":0.01, "round": False}),
            }
        }

    CATEGORY = "StableSwarmUI/masks"
    RETURN_TYPES = ("MASK",)
    FUNCTION = "seg"

    def seg(self, images, match_text, threshold):
        # TODO: Batch support?
        i = 255.0 * images[0].cpu().numpy()
        img = Image.fromarray(np.clip(i, 0, 255).astype(np.uint8))
        # TODO: Cache the model in RAM in some way?
        processor = CLIPSegProcessor.from_pretrained("CIDAS/clipseg-rd64-refined")
        model = CLIPSegForImageSegmentation.from_pretrained("CIDAS/clipseg-rd64-refined")
        with torch.no_grad():
            mask = model(**processor(text=match_text, images=img, return_tensors="pt", padding=True))[0]
        mask = torch.nn.functional.threshold(mask.sigmoid(), threshold, 0)
        mask -= mask.min()
        max = mask.max()
        if max > 0:
            mask /= max
        while mask.ndim < 4:
            mask = mask.unsqueeze(0)
        mask = torch.nn.functional.interpolate(mask, size=(images.shape[1], images.shape[2]), mode="bilinear").squeeze(0).squeeze(0)
        return (mask,)

NODE_CLASS_MAPPINGS = {
    "SwarmClipSeg": SwarmClipSeg,
}
