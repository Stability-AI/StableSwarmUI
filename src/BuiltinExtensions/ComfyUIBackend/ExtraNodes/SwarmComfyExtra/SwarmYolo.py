import torch, folder_paths
from PIL import Image
import numpy as np
from ultralytics import YOLO

class SwarmYoloDetection:
    @classmethod
    def INPUT_TYPES(cls):
        return {
            "required": {
                "image": ("IMAGE",),
                "model_name": (folder_paths.get_filename_list("yolov8"), ),
                "index": ("INT", { "default": 0, "min": 0, "max": 256, "step": 1 }),
            },
        }

    CATEGORY = "StableSwarmUI/masks"
    RETURN_TYPES = ("MASK",)
    FUNCTION = "seg"

    def seg(self, image, model_name, index):
        # TODO: Batch support?
        i = 255.0 * image[0].cpu().numpy()
        img = Image.fromarray(np.clip(i, 0, 255).astype(np.uint8))
        # TODO: Cache the model in RAM in some way?
        model = YOLO(folder_paths.get_full_path("yolov8", model_name))
        results = model(img)
        masks = results[0].masks.data
        if index == 0:
            return (masks, )
        elif index >= len(masks):
            return (torch.zeros_like(masks[0]), )
        else:
            return (masks[index], )

NODE_CLASS_MAPPINGS = {
    "SwarmYoloDetection": SwarmYoloDetection,
}
