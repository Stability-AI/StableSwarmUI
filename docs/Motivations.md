# Motivations

This document explains the reasoning behind some core choices for the design of the project. These are not necessarily sweeping statements, reasons to make the same choices, or even absolutely true facts - these are the reasons that choices were made, and nothing more.

## Language Choice

This project is built with a C# backend server to maximize performance while only minimally increasing code complexity. While most ML projects tend to be written in Python, that language is simply insufficient to meet the performance goals of this project\* (ie to provide a very fast and responsive multi-user-ready multi-backend service), notably it lacks "true" multithreading capabilities (due to Python GIL), which was deemed strongly necessary for StableSwarmUI (it must be able to use available CPU cores while serving user requests and managing internal data to be able to respond to all requests as quickly as possible).

It is also hoped that building Stable Diffusion tools in C# will enable a wider range of developers to make use of Stable Diffusion (vs being limited to the Python ecosystem).

\* This is not meant to be an insult to python, simply an explanation of why C# was chosen as being more ideal for the goals of the project than python is. [Some users have disagreed](https://github.com/Stability-AI/StableSwarmUI/issues/3) with this reasoning.

## Modularity

The project was designed to be heavily modular, such that backends are fully separated from the middle-layer which is fully separated from the frontend UI, and all components are interswappable. This is to enable extensibility and customization. For example, an extension can easily provide alternative backend generators (this project comes with several built-in, such as ComfyUI, Auto WebUI, StabilityAPI, ...) without having to edit anything else to work.

The limitation of this approach is some tools may not easily be intercompatible, eg the StabilityAPI backend has only a select few limited inputs, vs the local backends that have a wider range, and many parameters don't work with StabilityAPI.

## Comfy

For the goal of maximizing capabilities, a 'main' backend needed to be chosen to focus initial development on. ComfyUI was chosen because:
- It is itself an extremely modular and extensible system.
- It is highly performant and compatible.
- The code inside is extremely clean and well written.
- It provides bonus features that other UIs can't match (ie: the workflow node editor).
- The lead developer of Comfy was hired to Stability, and was able to directly help in ensuring the StableSwarmUI-ComfyUI integration works as best it can.

## Web Frontend

A completely custom HTML/JS frontend was built with the goal of allowing detailed and thorough customization of the UI (as opposed to eg being locked in to the way Gradio generates things).

Non-web based solutions are possible, but, well, ... everyone does webapps these days, and so all the convenient tools are built for webapps, and it's really nice for things like remote hosting the UI itself (and in the future for sharing it). So, wasn't really a choice, webapp frontend was the way to go.
