#!/bin/bash

ComfyDirectory=ComfyUI

mkdir -p dlbackend
cd dlbackend

[ -d $ComfyDirectory ] && rm -rf $ComfyDirectory
git clone https://github.com/comfyanonymous/ComfyUI $ComfyDirectory
cd $ComfyDirectory

python3 -m venv venv

pip install -r requirements.txt
