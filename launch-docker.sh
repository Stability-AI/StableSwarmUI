#!/usr/bin/env bash

docker build -t stableswarmui .

docker run -it \
    --mount source=swarmdata,target=/Data \
    --mount source=swarmbackend,target=/dlbackend \
    -v ./Models:/Models \
    -v ./Output:/Output \
    --gpus=all -p 7801:7801 stableswarmui
