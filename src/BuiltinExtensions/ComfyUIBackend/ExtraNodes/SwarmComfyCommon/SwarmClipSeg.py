import torch
from PIL import Image
import numpy as np
from transformers import CLIPSegProcessor, CLIPSegForImageSegmentation
import folder_paths
import os, requests

def get_path():
    if "clipseg" in folder_paths.folder_names_and_paths:
        paths = folder_paths.folder_names_and_paths["clipseg"]
        return paths[0][0]
    else:
        # Jank backup path if you're not running properly in Swarm
        path = os.path.dirname(os.path.realpath(__file__)) + "/models"
        return path


# Manual download of the model from a safetensors conversion.
# Done manually to guarantee it's only a safetensors file ever and not a pickle
def download_model(path, urlbase):
    if os.path.exists(path):
        return
    for file in ["config.json", "merges.txt", "model.safetensors", "preprocessor_config.json", "special_tokens_map.json", "tokenizer_config.json", "vocab.json"]:
        os.makedirs(path, exist_ok=True)
        filepath = path + file
        if not os.path.exists(filepath):
            with open(filepath, "wb") as f:
                print(f"[SwarmClipSeg] Downloading '{file}'...")
                f.write(requests.get(f"{urlbase}{file}").content)


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
        path = get_path() + "/clipseg-rd64-refined-fp16-safetensors/"
        download_model(path, "https://huggingface.co/mcmonkey/clipseg-rd64-refined-fp16/resolve/main/")
        processor = CLIPSegProcessor.from_pretrained(path)
        model = CLIPSegForImageSegmentation.from_pretrained(path)
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
