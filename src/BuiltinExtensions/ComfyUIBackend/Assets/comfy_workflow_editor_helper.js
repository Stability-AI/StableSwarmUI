
let hasComfyLoaded = false;

document.getElementById('maintab_comfyworkfloweditor').addEventListener('click', () => {
    if (hasComfyLoaded) {
        return;
    }
    hasComfyLoaded = true;
    let container = getRequiredElementById('comfy_workflow_frameholder');
    container.innerHTML = `<iframe class="comfy_workflow_frame" id="comfy_workflow_frame" src="/ComfyBackendDirect/"></iframe>`;
});
