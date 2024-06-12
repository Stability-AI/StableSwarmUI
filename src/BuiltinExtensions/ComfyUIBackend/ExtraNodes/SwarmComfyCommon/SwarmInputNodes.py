from . import SwarmLoadImageB64
import folder_paths
from nodes import CheckpointLoaderSimple

INT_MAX = 0xffffffffffffffff
INT_MIN = -INT_MAX

class SwarmInputGroup:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "title": ("STRING", {"default": "My Group"}),
                "open_by_default": ("BOOLEAN", {"default": True}),
                "description": ("STRING", {"default": "", "multiline": True}),
                "order_priority": ("FLOAT", {"default": 0, "min": -1024, "max": 1024, "step": 0.5, "round": False}),
                "is_advanced": ("BOOLEAN", {"default": False}),
                "can_shrink": ("BOOLEAN", {"default": True}),
            },
        }

    CATEGORY = "StableSwarmUI/inputs"
    RETURN_TYPES = ("GROUP",)
    FUNCTION = "do_input"

    def do_input(self, **kwargs):
        return (None, )


STANDARD_REQ_INPUTS = {
    "description": ("STRING", {"default": "", "multiline": True}),
    "order_priority": ("FLOAT", {"default": 0, "min": -1024, "max": 1024, "step": 0.5, "round": False}),
    "is_advanced": ("BOOLEAN", {"default": False}),
    "raw_id": ("STRING", {"default": ""}),
}
STANDARD_OTHER_INPUTS = {
    "optional": {
        "group": ("GROUP", )
    }
}


class SwarmInputInteger:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "title": ("STRING", {"default": "My Integer"}),
                "value": ("INT", {"default": 0, "min": INT_MIN, "max": INT_MAX, "step": 1}),
                "step": ("INT", {"default": 1, "min": INT_MIN, "max": INT_MAX, "step": 1}),
                "min": ("INT", {"default": 0, "min": INT_MIN, "max": INT_MAX, "step": 1}),
                "max": ("INT", {"default": 100, "min": INT_MIN, "max": INT_MAX, "step": 1}),
                "view_max": ("INT", {"default": 100, "min": INT_MIN, "max": INT_MAX, "step": 1}),
                "view_type": (["big", "small", "seed", "slider", "pot_slider"],),
            } | STANDARD_REQ_INPUTS,
        } | STANDARD_OTHER_INPUTS

    CATEGORY = "StableSwarmUI/inputs"
    RETURN_TYPES = ("INT",)
    FUNCTION = "do_input"

    def do_input(self, value, **kwargs):
        return (value, )


class SwarmInputFloat:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "title": ("STRING", {"default": "My Floating-Point Number"}),
                "value": ("FLOAT", {"default": 0, "min": INT_MIN, "max": INT_MAX, "step": 0.1, "round": False}),
                "step": ("FLOAT", {"default": 0.1, "min": INT_MIN, "max": INT_MAX, "step": 0.01, "round": False}),
                "min": ("FLOAT", {"default": 0, "min": INT_MIN, "max": INT_MAX, "step": 0.1, "round": False}),
                "max": ("FLOAT", {"default": 100, "min": INT_MIN, "max": INT_MAX, "step": 0.1, "round": False}),
                "view_max": ("FLOAT", {"default": 100, "min": INT_MIN, "max": INT_MAX, "step": 0.1, "round": False}),
                "view_type": (["big", "small", "seed", "slider", "pot_slider"],),
            } | STANDARD_REQ_INPUTS,
        } | STANDARD_OTHER_INPUTS

    CATEGORY = "StableSwarmUI/inputs"
    RETURN_TYPES = ("FLOAT",)
    FUNCTION = "do_input"

    def do_input(self, value, **kwargs):
        return (value, )


class SwarmInputText:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "title": ("STRING", {"default": "My Text"}),
                "value": ("STRING", {"default": "", "multiline": True}),
                "view_type": (["normal", "prompt"],),
            } | STANDARD_REQ_INPUTS,
        } | STANDARD_OTHER_INPUTS

    CATEGORY = "StableSwarmUI/inputs"
    RETURN_TYPES = ("STRING",)
    FUNCTION = "do_input"

    def do_input(self, value, **kwargs):
        return (value, )


class SwarmInputModelName:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "title": ("STRING", {"default": "My Model Name Input"}),
                "value": ("STRING", {"default": "", "multiline": False}),
                "subtype": (["Stable-Diffusion", "VAE", "LoRA", "Embedding", "ControlNet", "ClipVision"],),
            } | STANDARD_REQ_INPUTS,
        } | STANDARD_OTHER_INPUTS

    CATEGORY = "StableSwarmUI/inputs"
    RETURN_TYPES = ("",)
    FUNCTION = "do_input"

    def do_input(self, value, **kwargs):
        return (value, )


class SwarmInputCheckpoint:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "title": ("STRING", {"default": "My Checkpoint Model Name Input"}),
                "value": (folder_paths.get_filename_list("checkpoints"),),
            } | STANDARD_REQ_INPUTS,
        } | STANDARD_OTHER_INPUTS

    CATEGORY = "StableSwarmUI/inputs"
    RETURN_TYPES = ("MODEL", "CLIP", "VAE")
    FUNCTION = "do_input"

    def do_input(self, value, **kwargs):
        return CheckpointLoaderSimple().load_checkpoint(value)


class SwarmInputDropdown:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "title": ("STRING", {"default": "My Dropdown"}),
                "value": ("STRING", {"default": "", "multiline": False}),
                "values": ("STRING", {"default": "one, two, three", "multiline": True}),
            } | STANDARD_REQ_INPUTS,
        } | STANDARD_OTHER_INPUTS

    CATEGORY = "StableSwarmUI/inputs"
    RETURN_TYPES = ("STRING", "",)
    FUNCTION = "do_input"

    def do_input(self, value, **kwargs):
        return (value, value, )


class SwarmInputBoolean:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "title": ("STRING", {"default": "My Boolean"}),
                "value": ("BOOLEAN", {"default": False}),
            } | STANDARD_REQ_INPUTS,
        } | STANDARD_OTHER_INPUTS

    CATEGORY = "StableSwarmUI/inputs"
    RETURN_TYPES = ("BOOLEAN",)
    FUNCTION = "do_input"

    def do_input(self, value, **kwargs):
        return (value, )


class SwarmInputImage:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "title": ("STRING", {"default": "My Image"}),
                "value": ("STRING", {"default": "(Do Not Set Me)", "multiline": True}),
                "auto_resize": ("BOOLEAN", {"default": True}),
            } | STANDARD_REQ_INPUTS,
        } | STANDARD_OTHER_INPUTS

    CATEGORY = "StableSwarmUI/inputs"
    RETURN_TYPES = ("IMAGE","MASK",)
    FUNCTION = "do_input"

    def do_input(self, value, **kwargs):
        return SwarmLoadImageB64.b64_to_img_and_mask(value)


NODE_CLASS_MAPPINGS = {
    "SwarmInputGroup": SwarmInputGroup,
    "SwarmInputInteger": SwarmInputInteger,
    "SwarmInputFloat": SwarmInputFloat,
    "SwarmInputText": SwarmInputText,
    "SwarmInputModelName": SwarmInputModelName,
    "SwarmInputCheckpoint": SwarmInputCheckpoint,
    "SwarmInputDropdown": SwarmInputDropdown,
    "SwarmInputBoolean": SwarmInputBoolean,
    "SwarmInputImage": SwarmInputImage,
}
