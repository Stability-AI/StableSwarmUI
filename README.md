# StableSwarmUI

**StableSwarmUI 0.6.0 Alpha**.

A Modular Stable Diffusion Web-User-Interface, with an emphasis on making powertools easily accessible, high performance, and extensibility.

![ui-screenshot](.github/images/stableswarmui.jpg)

Follow the [Feature Announcements Thread](https://github.com/Stability-AI/StableSwarmUI/discussions/11) for updates on new features.

# Status

This project is in **ALPHA** status, preparing to move to **BETA** very soon. That means most things work, but there's a lot more planned before it's truly "ready for primetime". it's currently at a point where it's safe to recommend for general users, though you may need at times to use the integrated Comfy tab as a backup way to execute some things that would ideally be on the main UI. There are some known bugs and quality-of-life limits still be worked out.

Those interested in helping push to a Full ready-for-anything Release status are welcome to submit PRs (read the [Contributing](/CONTRIBUTING.md) document first), and you can contact us here on GitHub or on [Discord](https://discord.gg/stablediffusion) in the `#stableswarmui` channel. I highly recommended reaching out to ask about plans for a feature before PRing it. There may already be specific plans or even a work in progress.

Key feature targets not yet implemented:
- Localization / automatic translation
- Mobile browser formatting
- Self-contained internal mini-backend for simple usages
- full detail "Current Model" display in UI, separate from the model selector (probably as a tab within the batch sidebar?)

# Installing on Windows

- Download [The Install-Windows.bat file](https://github.com/Stability-AI/StableSwarmUI/releases/download/0.5.8-Alpha/install-windows.bat), store it somewhere you want to install at (not `Program Files`), and run it.
    - It should open a command prompt and install itself.
    - If it closes without going further, try running it again, it sometimes needs to run twice. (TODO: Fix that)
    - It will place an icon on your desktop that you can use to re-launch the server at any time.
    - When the installer completes, it will automatically launch the StableSwarmUI server, and open a browser window to the install page.
    - Follow the install instructions on the page.
    - After you submit, be patient, some of the install processing take a few minutes (downloading models and etc).

(TODO): Even easier self-contained pre-installer, a `.msi` or `.exe` that provides a general install screen and lets you pick folder and all.

# Installing on Linux

- Install `git`, `python3` via your OS package manager if they are not already installed (make sure to include `pip` and `venv` on distros that do not include them in python directly)
    - For example, on recent Ubuntu versions, `sudo apt install git python3-pip python3-venv`
- Install DotNET 7 using the instructions at https://dotnet.microsoft.com/en-us/download/dotnet/7.0 (you need `dotnet-sdk-7.0`, as that includes all relevant sub-packages)
    - Some users [have said](https://github.com/Stability-AI/StableSwarmUI/pull/6) that certain Linux distros expect `aspnet-runtime` to be installed separately
- In the near future, Swarm will update to DotNET 8, so to be ready for that: Install DotNET 8 using the instructions at https://dotnet.microsoft.com/en-us/download/dotnet/8.0 (you need `dotnet-sdk-8.0`, as that includes all relevant sub-packages)
- Open a shell terminal and `cd` to a directory you want to install into
- Run shell commands:
    - `git clone https://github.com/Stability-AI/StableSwarmUI`
    - cd `StableSwarmUI`
    - `./launch-linux.sh`
- open `http://localhost:7801/Install`
- Follow the install instructions on-page.

(TODO): Maybe outlink a dedicated document with per-distro details and whatever. Maybe also make a one-click installer for Linux?

# Installing on Mac

> **Note**: You can only run StableSwarmUI on Mac computers with M1 or M2 (Mx) Apple silicon processors.

1. Open Terminal.
2. Ensure your `brew` packages are updated with `brew update`.
3. Verify your `brew` installation with `brew doctor`. You should not see any error in the command output.
4. Install .NET for macOS: `brew install dotnet`.
5. Change the directory (`cd`) to the folder where you want to install StableSwarmUI.
6. Clone the StableSwarmUI GitHub repository: `git clone https://github.com/Stability-AI/StableSwarmUI`.
7. `cd StableSwarmUI` and run the installation script: `./launch-macos.sh`.

The installation starts now and downloads the Stable Diffusion models from the internet. Depending on your internet connection, this may take several minutes. Wait for your web browser to open the StableSwarmUI window.

> During the StableSwarmUI installation, you are prompted for the type of backend you want to use. For Mac computers with M1 or M2, you can safely choose the ComfyUI backend and choose the Stable Diffusion XL Base and Refiner models in the Download Models screen.

# Running with Docker

- To forward an Nvidia GPU, you must have the Nvidia Container Toolkit installed: https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html
- Open a shell terminal and `cd` to a directory you want to install into
- Run shell commands:
    - `git clone https://github.com/Stability-AI/StableSwarmUI`
    - cd `StableSwarmUI`
    - `./launch-docker.sh`
    - Open your browser to `localhost:7801`
- Note that it will forward the `Models` and `Output` directory, and will mount `Data` and `dlbackend` as independent persistent volumes.

# Documentation

See [the documentation folder](docs).

# Motivations

The "Swarm" name is in reference to the original key function of the UI: enabling a 'swarm' of GPUs to all generate images for the same user at once (especially for large grid generations). This is just the feature that inspired the name and not the end all of what Swarm is.

The overall goal of StableSwarmUI is to a be full-featured one-stop-shop for all things Stable Diffusion.

See [the motivations document](/docs/Motivations.md) for motivations on technical choices.

# Legal

This project:
- embeds a copy of [7-zip](https://7-zip.org/download.html) (LGPL).
- has the ability to auto-install [ComfyUI](https://github.com/comfyanonymous/ComfyUI) (GPL).
- has the option to use as a backend [AUTOMATIC1111/stable-diffusion-webui](https://github.com/AUTOMATIC1111/stable-diffusion-webui) (AGPL).
- can automatically install [christophschuhmann/improved-aesthetic-predictor](https://github.com/christophschuhmann/improved-aesthetic-predictor) (Apache2).
- can automatically install [yuvalkirstain/PickScore](https://github.com/yuvalkirstain/PickScore) (MIT).
- can automatically install [git-for-windows](https://git-scm.com/download/win) (GPLv2).
- uses [JSON.NET](https://github.com/JamesNK/Newtonsoft.Json) (MIT), [FreneticUtilities](https://github.com/FreneticLLC/FreneticUtilities) (MIT), [LiteDB](https://github.com/mbdavid/LiteDB) (MIT), [ImageSharp](https://github.com/SixLabors/ImageSharp/) (Apache2 under open-source Split License)
- embeds copies of web assets from [BootStrap](https://getbootstrap.com/) (MIT), [Select2](https://select2.org/) (MIT), [JQuery](https://jquery.com/) (MIT), [exifr](https://github.com/MikeKovarik/exifr) (MIT).
- has the option to connect to remote servers to use [the Stability.ai API](https://dreamstudio.com/api/start/) as a backend.
- supports user-built extensions which may have their own licenses or legal conditions.

Copyright (C) 2023 Stability.AI

Licensed under the PolyForm Noncommercial License 1.0.0, view [LICENSE.txt](/LICENSE.txt) for details.

(In short: personal usage is free, commercial usage is not)

This license is an initial selection, particularly for the Alpha stage of the project, that is subject to change in the future.
It will probably be swapped to MIT during Beta.
