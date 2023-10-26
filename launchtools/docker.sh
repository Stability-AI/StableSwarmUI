#!/usr/bin/env bash

# Launch as normal, just ensure launch mode is off and host is global (to expose it out of the container)
bash ./launch-linux.sh --launch_mode none --host 0.0.0.0
