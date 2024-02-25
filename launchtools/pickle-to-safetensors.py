# Internally called by StableSwarmUI
# python -s launchtools/pickle-to-safetensors.py <path> <fp16(true/false)>

import os, sys, glob, traceback

sys.path.append(os.path.dirname(__file__))

try:
    import torch
except ImportError:
    os.system('python -m pip install torch')
try:
    import safetensors
except ImportError:
    os.system('python -m pip install safetensors')
import torch
from safetensors.torch import save_file

fp16 = sys.argv[2].lower() == 'true'

path = sys.argv[1]
def get_all(ext):
    return glob.glob(f"{path}/**/*.{ext}", recursive=True)
files = get_all('ckpt') + get_all('pt') + get_all('bin') + get_all('pth')

import pickle_module

for file in files:
    try:
        if '/backups/' in file.replace('\\', '/'):
            continue
        print(f"Will convert {file}...")
        last_dot = file.rindex('.')
        fname_clean = file[:last_dot]
        with open(file, 'rb') as f:
            tens = torch.load(f, map_location='cpu', pickle_module=pickle_module)
            metadata = {}
            # Stable-Diffusion checkpoint model data
            if "state_dict" in tens:
                tens = tens["state_dict"]
            # TI Embedding data
            if "string_to_param" in tens:
                vals = next(iter(tens["string_to_param"].values()))
                if isinstance(vals, torch.nn.ParameterDict):
                    vals = {k: v.data for k, v in vals.items()}
                if isinstance(vals, torch.nn.Parameter):
                    vals = vals.data
                tens["emb_params"] = vals
                del tens["string_to_param"]
            if "name" in tens:
                name = str(tens["name"])
                if name:
                    metadata["modelspec.title"] = name
                    del tens["name"]
            if "sd_checkpoint" in tens:
                ckpt_name = str(tens["sd_checkpoint"])
                if ckpt_name:
                    metadata["modelspec.description"] = f"Embedding trained against '{ckpt_name}'"
                    del tens["sd_checkpoint"]
            if "sd_checkpoint_name" in tens:
                ckpt_name = str(tens["sd_checkpoint_name"])
                if ckpt_name:
                    metadata["modelspec.description"] = f"Embedding trained against '{ckpt_name}'"
                    del tens["sd_checkpoint_name"]
            # General cleanup
            for k, v in dict(tens).items():
                if k.startswith("loss."): # VAE stray data
                    del tens[k]
                elif k.startswith("model_ema."): # Stable-Diffusion checkpoint model stray data
                    del tens[k]
                elif type(v) != torch.Tensor:
                    raw_data = str(v)
                    if (len(raw_data) > 100):
                        raw_data = raw_data[:100] + "..."
                    print(f"discard {k} = {raw_data}")
                    del tens[k]
                elif fp16:
                    tens[k] = tens[k].half()
            save_file(tens, fname_clean + '.safetensors', metadata=metadata)
        rel = os.path.relpath(file, path)
        os.makedirs(os.path.dirname(f"{path}/backups/{rel}"), exist_ok=True)
        os.rename(file, f"{path}/backups/{rel}")
    except Exception as e:
        print(f"Failed to convert {file}:")
        traceback.print_exc()
