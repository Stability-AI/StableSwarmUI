#!/bin/bash

mkdir dlbackend

cd dlbackend

git clone https://github.com/comfyanonymous/ComfyUI

cd ComfyUI

if [ -z "${SWARM_NO_VENV}" ]; then

    python3 -s -m venv venv

    . venv/bin/activate
fi

python3 -s -m pip install torch torchvision torchaudio --extra-index-url https://download.pytorch.org/whl/cu121
python3 -s -m pip install -r requirements.txt
