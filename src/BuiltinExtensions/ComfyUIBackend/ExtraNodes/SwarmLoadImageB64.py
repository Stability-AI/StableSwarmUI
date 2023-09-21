from PIL import Image, ImageOps
import numpy as np
import torch, base64, io

class SwarmLoadImageB64:
    @classmethod
    def INPUT_TYPES(s):
        return {"required":
                    {"image_base64": ("STRING", {"multiline": True})},
                }

    CATEGORY = "StableSwarmUI"
    RETURN_TYPES = ("IMAGE",)
    FUNCTION = "load_image_b64"

    def load_image_b64(self, image_base64):
        imageData = base64.b64decode(image_base64)
        i = Image.open(io.BytesIO(imageData))
        i = ImageOps.exif_transpose(i)
        image = i.convert("RGB")
        image = np.array(image).astype(np.float32) / 255.0
        image = torch.from_numpy(image)[None,]
        return (image,)

NODE_CLASS_MAPPINGS = {
    "SwarmLoadImageB64": SwarmLoadImageB64,
}
