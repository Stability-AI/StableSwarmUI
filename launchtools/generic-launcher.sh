#!/usr/bin/env bash

export CUDA_VISIBLE_DEVICES=$1
export COMMANDLINE_ARGS=$4

cd "$2"

"$3" $4
