#!/bin/bash

mkdir dlbackend

cd dlbackend

git clone https://github.com/comfyanonymous/ComfyUI

cd ComfyUI

python3 -m venv venv

. venv/bin/activate

pip install -r requirements.txt
