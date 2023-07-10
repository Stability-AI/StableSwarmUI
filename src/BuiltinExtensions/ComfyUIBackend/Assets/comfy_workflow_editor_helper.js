
let hasComfyLoaded = false;

document.getElementById('maintab_comfyworkfloweditor').addEventListener('click', () => {
    if (hasComfyLoaded) {
        return;
    }
    hasComfyLoaded = true;
    let container = document.getElementById('comfy_workflow_editor_container');
    container.innerHTML = `<iframe id="comfy_workflow_frame" src="/ComfyBackendDirect"></iframe>`;
});
