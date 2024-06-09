from PIL import Image
import numpy as np
import comfy.utils
from server import PromptServer, BinaryEventTypes
import time, io, struct

SPECIAL_ID = 12345 # Tells swarm that the node is going to output final images
VIDEO_ID = 12346

class SwarmSaveImageWS:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "images": ("IMAGE", )
            }
        }

    CATEGORY = "StableSwarmUI/images"
    RETURN_TYPES = ()
    FUNCTION = "save_images"
    OUTPUT_NODE = True

    def save_images(self, images):
        pbar = comfy.utils.ProgressBar(SPECIAL_ID)
        step = 0
        for image in images:
            i = 255.0 * image.cpu().numpy()
            img = Image.fromarray(np.clip(i, 0, 255).astype(np.uint8))
            pbar.update_absolute(step, SPECIAL_ID, ("PNG", img, None))
            step += 1

        return {}

    @classmethod
    def IS_CHANGED(s, images):
        return time.time()


class SwarmSaveAnimatedWebpWS:
    methods = {"default": 4, "fastest": 0, "slowest": 6}

    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "images": ("IMAGE", ),
                "fps": ("FLOAT", {"default": 6.0, "min": 0.01, "max": 1000.0, "step": 0.01}),
                "lossless": ("BOOLEAN", {"default": True}),
                "quality": ("INT", {"default": 80, "min": 0, "max": 100}),
                "method": (list(s.methods.keys()),),
            },
        }

    CATEGORY = "StableSwarmUI/video"
    RETURN_TYPES = ()
    FUNCTION = "save_images"
    OUTPUT_NODE = True

    def save_images(self, images, fps, lossless, quality, method):
        method = self.methods.get(method)
        pil_images = []
        for image in images:
            i = 255. * image.cpu().numpy()
            img = Image.fromarray(np.clip(i, 0, 255).astype(np.uint8))
            pil_images.append(img)

        out = io.BytesIO()
        type_num = 3
        header = struct.pack(">I", type_num)
        out.write(header)
        pil_images[0].save(out, save_all=True, duration=int(1000.0/fps), append_images=pil_images[1 : len(pil_images)], lossless=lossless, quality=quality, method=method, format='WEBP')
        out.seek(0)
        preview_bytes = out.getvalue()
        server = PromptServer.instance
        server.send_sync("progress", {"value": 12346, "max": 12346}, sid=server.client_id)
        server.send_sync(BinaryEventTypes.PREVIEW_IMAGE, preview_bytes, sid=server.client_id)

        return { }

    @classmethod
    def IS_CHANGED(s, images, fps, lossless, quality, method):
        return time.time()


NODE_CLASS_MAPPINGS = {
    "SwarmSaveImageWS": SwarmSaveImageWS,
    "SwarmSaveAnimatedWebpWS": SwarmSaveAnimatedWebpWS,
}
