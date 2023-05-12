#!/usr/bin/env bash

# Ensure correct local path.
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
cd $SCRIPT_DIR

# Building first is more reliable than running directly from src
dotnet build
# Default env configuration, gets overwritten by the C# code's settings handler
ASPNETCORE_ENVIRONMENT="Production"
ASPNETCORE_URLS="http://*:7801"
# Actual runner.
dotnet src/bin/Debug/net7.0/StableUI.dll $@
