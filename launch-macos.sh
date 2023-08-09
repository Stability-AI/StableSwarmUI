#!/usr/bin/env bash

# Ensure correct local path.
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
cd $SCRIPT_DIR

# Building first is more reliable than running directly from src
dotnet build src/StableSwarmUI.csproj --configuration Release -o ./src/bin/live_release
# Default env configuration, gets overwritten by the C# code's settings handler
ASPNETCORE_ENVIRONMENT="Production"
ASPNETCORE_URLS="http://*:7801"

# PyTorch MPS fallback to CPU, so incompatible comfy nodes can still work.
export PYTORCH_ENABLE_MPS_FALLBACK=1

# Actual runner.
dotnet src/bin/live_release/StableSwarmUI.dll $@
