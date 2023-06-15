#!/usr/bin/env bash

export CUDA_VISIBLE_DEVICES=$1
export COMMANDLINE_ARGS=$4

cd "$2"

if [[ $5 -eq py ]]; then
    if test -f "$2/venv/bin/activate"; then
        source "$2/venv/bin/activate"
    fi
    python3 "$3" $4
else
    "$3" $4
fi
