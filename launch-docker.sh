#!/usr/bin/env bash

# Ensure correct local path.
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
cd $SCRIPT_DIR

sed -i 's/webinstall/none/g' ./src/Core/Settings.cs

# Building first is more reliable than running directly from src
dotnet build src/StableSwarmUI.csproj --configuration Release -o ./src/bin/live_release
# Default env configuration, gets overwritten by the C# code's settings handler
ASPNETCORE_ENVIRONMENT="Production"
ASPNETCORE_URLS="http://*:7801"
cp -R ./src/* /publish/
dotnet publish --os linux --arch x64 -c Release --property:PublishDir=/publish #/t:PublishContainer -c Release
