# Scorers Extension

Adds image-scoring tools (PickScore, Aesthetic scores).

These by default get stored in the image metadata, but can then be further processed from there (for example, the Grid Generator tool can display heatmaps of the scores)

Currently, this uses the local current default GPU. TODO: This should have a backend registration system just like T2I does (and/or get integrated into the self-contained tooling).
