import comfy.model_management
import safetensors.torch
import torch, os, comfy, json

# ATTRIBUTION: This code is a mix of code from kohya-ss, comfy, and Swarm. It would be annoying to disentangle but it's all FOSS and relatively short so it's fine.

CLAMP_QUANTILE = 0.99
def extract_lora(diff, rank):
    conv2d = (len(diff.shape) == 4)
    kernel_size = None if not conv2d else diff.size()[2:4]
    conv2d_3x3 = conv2d and kernel_size != (1, 1)
    out_dim, in_dim = diff.size()[0:2]
    rank = min(rank, in_dim, out_dim)

    if conv2d:
        if conv2d_3x3:
            diff = diff.flatten(start_dim=1)
        else:
            diff = diff.squeeze()

    U, S, Vh = torch.linalg.svd(diff.float())
    U = U[:, :rank]
    S = S[:rank]
    U = U @ torch.diag(S)
    Vh = Vh[:rank, :]

    dist = torch.cat([U.flatten(), Vh.flatten()])
    hi_val = torch.quantile(dist, CLAMP_QUANTILE)
    low_val = -hi_val

    U = U.clamp(low_val, hi_val)
    Vh = Vh.clamp(low_val, hi_val)
    if conv2d:
        U = U.reshape(out_dim, rank, 1, 1)
        Vh = Vh.reshape(rank, in_dim, kernel_size[0], kernel_size[1])
    return (U, Vh)


def do_lora_handle(base_data, other_data, rank, prefix, require, do_bias, callback):
    out_data = {}
    device = comfy.model_management.get_torch_device()
    for key in base_data.keys():
        callback()
        if key not in other_data:
            continue
        base_tensor = base_data[key]
        other_tensor = other_data[key]
        if key.startswith("clip_g"):
            key = "1." + key[len("clip_g."):]
        elif key.startswith("clip_l"):
            key = "0." + key[len("clip_l."):]
        if require:
            if not key.startswith(require):
                print(f"Ignore unmatched key {key} (doesn't match {require})")
                continue
            key = key[len(require):]
        if base_tensor.shape != other_tensor.shape:
            continue
        diff = other_tensor.to(device) - base_tensor.to(device)
        other_tensor = other_tensor.cpu()
        base_tensor = base_tensor.cpu()
        max_diff = float(diff.abs().max())
        if max_diff < 1e-5:
            print(f"discard unaltered key {key} ({max_diff})")
            continue
        if key.endswith(".weight"):
            fixed_key = key[:-len(".weight")].replace('.', '_')
            name = f"lora_{prefix}_{fixed_key}"
            if len(base_tensor.shape) >= 2:
                print(f"extract key {name} ({max_diff})")
                out = extract_lora(diff, rank)
                out_data[f"{name}.lora_up.weight"] = out[0].contiguous().half().cpu()
                out_data[f"{name}.lora_down.weight"] = out[1].contiguous().half().cpu()
            else:
                print(f"ignore valid raw pass-through key {name} ({max_diff})")
                #out_data[name] = other_tensor.contiguous().half().cpu()
        elif key.endswith(".bias") and do_bias:
            fixed_key = key[:-len(".bias")].replace('.', '_')
            name = f"lora_{prefix}_{fixed_key}"
            print(f"extract bias key {name} ({max_diff})")
            out_data[f"{name}.diff_b"] = diff.contiguous().half().cpu()


    return out_data

class SwarmExtractLora:
    def __init__(self):
        self.loaded_lora = None

    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "base_model": ("MODEL", ),
                "base_model_clip": ("CLIP", ),
                "other_model": ("MODEL", ),
                "other_model_clip": ("CLIP", ),
                "rank": ("INT", {"default": 16, "min": 1, "max": 320}),
                "save_rawpath": ("STRING", {"multiline": False}),
                "save_filename": ("STRING", {"multiline": False}),
                "save_clip": ("BOOLEAN", {"default": True}),
                "metadata": ("STRING", {"multiline": True}),
            }
        }

    CATEGORY = "StableSwarmUI/models"
    RETURN_TYPES = ()
    FUNCTION = "extract_lora"
    OUTPUT_NODE = True

    def extract_lora(self, base_model, base_model_clip, other_model, other_model_clip, rank, save_rawpath, save_filename, save_clip, metadata):
        base_data = base_model.model_state_dict()
        other_data = other_model.model_state_dict()
        key_count = len(base_data.keys())
        if save_clip:
            key_count += len(base_model_clip.get_sd().keys())
        pbar = comfy.utils.ProgressBar(key_count)
        class Helper:
            steps = 0
            def callback(self):
                self.steps += 1
                pbar.update_absolute(self.steps, key_count, None)
        helper = Helper()
        out_data = do_lora_handle(base_data, other_data, rank, "unet", "diffusion_model.", True, lambda: helper.callback())
        if save_clip:
            # TODO: CLIP keys get wonky, this probably doesn't work? Model-arch-dependent.
            out_clip = do_lora_handle(base_model_clip.get_sd(), other_model_clip.get_sd(), rank, "te_text_model_encoder_layers", "0.transformer.text_model.encoder.layers.", False, lambda: helper.callback())
            out_clip = do_lora_handle(base_model_clip.get_sd(), other_model_clip.get_sd(), rank, "te2_text_model_encoder_layers", "1.transformer.text_model.encoder.layers.", False, lambda: helper.callback())
            out_data.update(out_clip)

        # Can't easily autodetect all the correct modelspec info, but at least supply some basics
        out_metadata = {
            "modelspec.title": f"(Extracted LoRA) {save_filename}",
            "modelspec.description": f"LoRA extracted in StableSwarmUI"
        }
        if metadata:
            out_metadata.update(json.loads(metadata))
        path = f"{save_rawpath}{save_filename}.safetensors"
        print(f"saving to path {path}")
        safetensors.torch.save_file(out_data, path, metadata=out_metadata)
        return ()

NODE_CLASS_MAPPINGS = {
    "SwarmExtractLora": SwarmExtractLora,
}
