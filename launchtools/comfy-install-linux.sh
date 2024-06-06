#!/bin/bash

mkdir dlbackend

cd dlbackend

git clone https://github.com/comfyanonymous/ComfyUI

cd ComfyUI

python=`which python3`
if [ "$python" == "" ]; then
    >&2 echo ERROR: cannot find python3
    >&2 echo Please follow the install instructions in the readme!
    exit 1
fi

venv=`python3 -m venv 2>&1`
case $venv in
    *usage*)
        :
    ;;
    *)
        >&2 echo ERROR: python venv is not installed
        >&2 echo Please follow the install instructions in the readme!
        >&2 echo If on Ubuntu/Debian, you may need: sudo apt install python3-venv
        exit 1
    ;;
esac

if [ -z "${SWARM_NO_VENV}" ]; then

    python3 -s -m venv venv

    . venv/bin/activate
fi

python3 -s -m pip install torch torchvision torchaudio --extra-index-url https://download.pytorch.org/whl/cu121
python3 -s -m pip install -r requirements.txt
