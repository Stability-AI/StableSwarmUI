#!/usr/bin/env bash

export CUDA_VISIBLE_DEVICES=$1

cd "$2"

"$3" $4
