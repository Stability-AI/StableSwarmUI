# ComfyUI Backend Extension For StableSwarmUI

This extension enables the use of [ComfyUI](https://github.com/comfyanonymous/ComfyUI) as a backend provider for StableSwarmUI.

Among other benefits, this enables you to use custom ComfyUI-API workflow files within StableSwarmUI.

You can also view the ComfyUI node graph and work with custom workflows directly in the UI when any comfy backend is enabled.

### API vs Self-Start

- Self-Start lets swarm configure, launch, and manage the ComfyUI backend. This is highly recommended.
- API-By-URL is for if you want to launch and manage the ComfyUI instance entirely yourself, but still connect it from Swarm.
    - Configuration is significantly more complex, and misbehavior may occur. This is not recommended.

### Installation (Self-Start)

- First: Have a valid ComfyUI install. The StableSwarmUI installer automatically provides you one (if not disabled) as `dlbackend/comfy/ComfyUI/main.py`.
- Go to `Server` -> `Backends`, and click `ComfyUI Self-Starting`, and fill in the `StartScript` path as above. Other values can be left default or configured to your preference.
- Save the backend, and it should just work.

### Installation (API)

- First: have a valid and working ComfyUI installation.
- Make sure it uses the exact same model paths as your StableSwarmUI instance does. This means that if you have eg `OfficialStableDiffusion/sd_xl_base_1.0.safetensors` in Swarm, you need have *EXACTLY* that in ComfyUI. The only exception is Windows paths that use `\` instead of `/` are fine, Swarm will automatically correct for that (If you use Self-Start, this is automatically managed from your Swarm settings).
- Note that swarm may leave stray Input or Output images in the ComfyUI folder that you may wish to clean up (if you use Self-Start, this will be prevented automatically).
- Swarm provides extra Comfy nodes automatically to Self-Start ComfyUI instances from folders within the ComfyUI extension folder, including `DLNodes` and `ExtraNodes` - it is highly recommended you copy these to your remote Comfy's `custom_nodes` path.

### Basic Usage Within StableSwarmUI

(TODO)

### Using Workflows In The UI

(TODO): explain the Node tab and how to use it within StableSwarmUI, link out to Comfy docs for usage of the node editor itself.

- When using a custom workflow in the main Generate tab:
    - Default nodes (KSampler, LoadCheckpoint, etc) will automatically detect and link to standard Swarm workflows.
    - You can use the `SwarmLoraLoader` node to allow loading loras in your workflow, see [here](https://github.com/Stability-AI/StableSwarmUI/issues/130#issuecomment-1772718963)

### Making and Using Your Own Custom Workflow Files

(TODO): explain the API-specific workflow file format, how it differs from workflows in the UI, and how to use it.

(TODO): Are API-format custom workflows even relevant anymore? UI-workflows are easier and nicer.

(Note: this readme section should mention that the main checkpoint loader should be ID `4` for best compatibility, due to how ComfyUI loads models - see `just_load_model.json`)
