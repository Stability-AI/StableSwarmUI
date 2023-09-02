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
# note four lines below, this command happens twice for testing
cp -R /code/StableSwarmUI/src /publish/src
dotnet publish --os linux --arch x64 -c Release --property:PublishDir=/publish #/t:PublishContainer -c Release
# this might get wiped out if it happens before the publish step?
cp -R /code/StableSwarmUI/src /publish/src
