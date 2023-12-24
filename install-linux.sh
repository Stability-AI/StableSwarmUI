#!/usr/bin/env bash

# Ensure correct local path.
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
cd $SCRIPT_DIR

# Download swarm
git clone https://github.com/Stability-AI/StableSwarmUI
cd StableSwarmUI

# install dotnet
cd launchtools
rm dotnet-install.sh
# https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 7.0
./dotnet-install.sh --channel 8.0
cd ..

# Launch
./launch-linux.sh $@
