#!/bin/bash

mkdir dlbackend

cd dlbackend

git clone https://github.com/comfyanonymous/ComfyUI

cd ComfyUI

if [ -z "${SWARM_NO_VENV}" ]; then

    python3 -m venv venv

    . venv/bin/activate
fi

pip install -r requirements.txt
