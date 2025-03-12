#!/usr/bin/env bash

podman build -t stableswarmui .

# add "--network=host" if you want to access other services on the host network (eg a separated comfy instance)
podman run -it \
    --name stableswarmui \
    --mount type=bind,src=swarmdata,target=/Data \
    --mount type=bind,src=swarmbackend,target=/dlbackend \
    -v ./Models:/Models \
    -v ./Output:/Output \
    --device nvidia.com/gpu=all -p 7801:7801 stableswarmui

