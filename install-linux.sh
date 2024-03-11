#!/usr/bin/env bash

# Ensure correct local path.
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
cd $SCRIPT_DIR

# Accidental run prevention
if [ -d "StableSwarmUI" ]; then
    echo "StableSwarmUI already exists in this directory. Please remove it before installing."
    exit 1
fi
if [ -f "StableSwarmUI.sln" ]; then
    echo "StableSwarmUI already exists in this directory. Please remove it before installing."
    exit 1
fi

# Download swarm
git clone https://github.com/Stability-AI/StableSwarmUI
cd StableSwarmUI

# install dotnet
cd launchtools
rm dotnet-install.sh
# https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh

# Note: manual installers that want to avoid home dir, add to both of the below lines: --install-dir $SCRIPT_DIR/.dotnet
./dotnet-install.sh --channel 8.0 --runtime aspnetcore
./dotnet-install.sh --channel 8.0
cd ..

# Launch
./launch-linux.sh $@
