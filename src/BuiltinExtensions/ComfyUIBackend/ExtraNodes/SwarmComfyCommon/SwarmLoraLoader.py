import comfy
import folder_paths

class SwarmLoraLoader:
    def __init__(self):
        self.loaded_lora = None

    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "model": ("MODEL", ),
                "clip": ("CLIP", ),
                "lora_names": ("STRING", {"multiline": True}),
                "lora_weights": ("STRING", {"multiline": True})
            }
        }

    CATEGORY = "StableSwarmUI/models"
    RETURN_TYPES = ("MODEL", "CLIP")
    FUNCTION = "load_loras"

    def load_loras(self, model, clip, lora_names, lora_weights):
        if lora_names.strip() == "":
            return (model, clip)

        lora_names = lora_names.split(",")
        lora_weights = lora_weights.split(",")
        lora_weights = [float(x.strip()) for x in lora_weights]

        for i in range(len(lora_names)):
            lora_name = lora_names[i].strip()
            weight = lora_weights[i]
            if weight == 0:
                continue
            # This section copied directly from default comfy LoraLoader
            lora_path = folder_paths.get_full_path("loras", lora_name)
            lora = None
            if self.loaded_lora is not None:
                if self.loaded_lora[0] == lora_path:
                    lora = self.loaded_lora[1]
                else:
                    temp = self.loaded_lora
                    self.loaded_lora = None
                    del temp
            if lora is None:
                lora = comfy.utils.load_torch_file(lora_path, safe_load=True)
                self.loaded_lora = (lora_path, lora)
            model, clip = comfy.sd.load_lora_for_models(model, clip, lora, weight, weight)

        return (model, clip)

NODE_CLASS_MAPPINGS = {
    "SwarmLoraLoader": SwarmLoraLoader,
}
