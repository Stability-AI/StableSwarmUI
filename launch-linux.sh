#!/usr/bin/env bash

# Ensure correct local path.
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
cd $SCRIPT_DIR

# Add dotnet non-admin-install to path
export PATH="$SCRIPT_DIR/.dotnet:~/.dotnet:$PATH"

# Build the program if it isn't already built
if [ ! -f src/bin/live_release/StableSwarmUI.dll ]; then
    dotnet build src/StableSwarmUI.csproj --configuration Release -o ./src/bin/live_release
fi

# Default env configuration, gets overwritten by the C# code's settings handler
export ASPNETCORE_ENVIRONMENT="Production"
export ASPNETCORE_URLS="http://*:7801"
# Actual runner.
dotnet src/bin/live_release/StableSwarmUI.dll $@
