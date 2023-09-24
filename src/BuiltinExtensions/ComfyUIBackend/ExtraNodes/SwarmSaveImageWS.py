from PIL import Image
import numpy as np
import comfy.utils
import time

class SwarmSaveImageWS:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "images": ("IMAGE", )
            }
        }

    CATEGORY = "StableSwarmUI"
    RETURN_TYPES = ()
    FUNCTION = "save_images"
    OUTPUT_NODE = True

    def save_images(self, images):
        SPECIAL_ID = 12345 # Tells swarm that the node is going to output final images
        pbar = comfy.utils.ProgressBar(SPECIAL_ID)
        step = 0
        for image in images:
            i = 255. * image.cpu().numpy()
            img = Image.fromarray(np.clip(i, 0, 255).astype(np.uint8))
            pbar.update_absolute(step, SPECIAL_ID, ("PNG", img, None))
            step += 1

        return {}

    def IS_CHANGED(s, images):
        return time.time()

NODE_CLASS_MAPPINGS = {
    "SwarmSaveImageWS": SwarmSaveImageWS,
}
