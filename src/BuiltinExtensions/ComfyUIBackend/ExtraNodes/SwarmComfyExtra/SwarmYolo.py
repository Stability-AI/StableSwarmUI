import torch, folder_paths, comfy
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
        masks = results[0].masks
        if masks is None:
            boxes = results[0].boxes
            if boxes is None or len(boxes) == 0:
                return (torch.zeros(1, image.shape[1], image.shape[2]), )
            masks = torch.zeros((len(boxes), image.shape[1], image.shape[2]), dtype=torch.float32, device="cpu")
            for i, box in enumerate(boxes):
                x1, y1, x2, y2 = box.xyxy[0].tolist()
                masks[i, int(y1):int(y2), int(x1):int(x2)] = 1.0
        else:
            masks = masks.data.cpu()
        masks = torch.nn.functional.interpolate(masks.unsqueeze(1), size=(image.shape[1], image.shape[2]), mode="bilinear").squeeze(1)
        if index == 0:
            result = masks[0]
            for i in range(1, len(masks)):
                result = torch.max(result, masks[i])
            return (result, )
        elif index > len(masks):
            return (torch.zeros_like(masks[0]), )
        else:
            return (masks[index - 1].unsqueeze(0), )

NODE_CLASS_MAPPINGS = {
    "SwarmYoloDetection": SwarmYoloDetection,
}
