# Scorers Extension

Adds image-scoring tools (PickScore, Aesthetic scores).

These by default get stored in the image metadata, but can then be further processed from there (for example, the Grid Generator tool can display heatmaps of the scores)

Currently, this uses the local current default GPU. TODO: This should have a backend registration system just like T2I does (and/or get integrated into the self-contained tooling).

## Installation

TODO: This needs to automate itself way.

For now:

- You must have python3 installed
- open the `src/BuiltinExtensions/Scorers` folder
- Recommend that you create a venv
    - `python3 -m venv venv`
    - `source venv/bin/activate` for Linux, or `venv\Scripts\activate` for Windows
- `pip install -r requirements.txt`
- After that it should just work from the UI.
