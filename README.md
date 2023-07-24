# StableSwarmUI

A Modular Stable Diffusion Web-User-Interface, with an emphasis on making powertools easily accessible, high performance, and extensibility.

# Installing on Windows

(TODO): provide an easy installer link here

# Installing on Linux

- Install `git`, `python3`, `7zip` via your OS package manager if they are not already installed.
- Install DotNET 7 using the instructions at https://dotnet.microsoft.com/en-us/download/dotnet/7.0
- Open a shell terminal and `cd` to a directory you want to install into
- Run shell commands:
    - `git clone https://github.com/Stability-AI/StableSwarmUI`
    - cd `StableSwarmUI`
    - `./launch-linux.sh`
- open `http://localhost:7801/Install`
- Follow the install instructions on-page.

(TODO): Maybe outlink a dedicated document with per-distro details and whatever. Maybe also make a one-click installer for Linux?

# Installing on Mac

(TODO): somebody with Mac experience needs to fill this in. Probably similar to Linux.

# Documentation

See [the documentation folder](docs).

# Motivations

The "Swarm" name is in reference to the original key function of the UI: enabling a 'swarm' of GPUs to all generate images for the same user at once (especially for large grid generations).

This project is built with a C# backend server to maximize performance while only minimally increasing code complexity. While most ML projects tend to be written in Python, that language is simply insufficient to meet performance goals, notably it lacks "true" multithreading capabilities, which was deemed strongly necessary for StableSwarmUI. It is also hoped that building Stable Diffusion tools in C# will enable a wider range of developers to make use of Stable Diffusion (vs being limited to the Python ecosystem).

The project was designed to be heavily modular, such that backends are fully separated from the middle-layer which is fully separated from the frontend UI, and all components are interswappable. This is to enabled extensibility and customization. For example, an extension can easily provide alternative backend generators (this project comes with several built-in, such as ComfyUI, Auto WebUI, StabilityAPI, ...) without having to edit anything else to work. The limitation of this approach is some tools may not easily be intercompatible, eg the StabilityAPI backend has only a select few limited inputs, vs the local backends that have a wider range, and so many parameters don't work with StabilityAPI.

A completely custom HTML/JS frontend was built with the goal of allowing detailed and thorough customization of the UI (as opposed to eg being locked in to the way Gradio generates things).

# Legal

This project:
- embeds a copy of [7-zip](https://7-zip.org/download.html) (LGPL).
- has the ability to auto-install [ComfyUI](https://github.com/comfyanonymous/ComfyUI) (GPL).
- has the option to use as a backend [AUTOMATIC1111/stable-diffusion-webui](https://github.com/AUTOMATIC1111/stable-diffusion-webui) (AGPL).
- can automatically install [christophschuhmann/improved-aesthetic-predictor](https://github.com/christophschuhmann/improved-aesthetic-predictor) (Apache2).
- can automatically install [yuvalkirstain/PickScore](https://github.com/yuvalkirstain/PickScore) (MIT).
- can automatically install [git-for-windows](https://git-scm.com/download/win) (GPLv2).
- embeds copies of web assets from [BootStrap](https://getbootstrap.com/) (MIT), [Select2](https://select2.org/) (MIT), [JQuery](https://jquery.com/) (MIT).
- has the option to connect to remote servers to use [the Stability.ai API](https://dreamstudio.com/api/start/) as a backend.
- supports user-built extensions which may have their own licenses or legal conditions.

Created by Alex Goodwin for StabilityAI.
